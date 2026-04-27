using MacroscopTest.Services;
using MacroscopTest.Tests.TestHelpers;
using MacroscopTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MacroscopTest.Tests.ViewModels;

[TestClass]
public class ImageSlotViewModelTests
{
    #region Helpers

    private const string DefaultImageUrl = "https://example.com/image.png";
    private const string FirstImageUrl = "https://example.com/first.png";
    private const string SecondImageUrl = "https://example.com/second.png";
    private const string InvalidUrl = "invalid-url";

    private static ImageSlotViewModel CreateViewModel(FakeImageDownloadService? service = null)
    {
        service ??= new FakeImageDownloadService();

        return new ImageSlotViewModel(service, new FileLogger());
    }

    private static FakeImageDownloadService CreatePendingDownloadService(
        TaskCompletionSource<bool> started,
        TaskCompletionSource<DownloadedImage> completion)
    {
        return new FakeImageDownloadService
        {
            OnDownloadAsync = (_, token) =>
            {
                started.TrySetResult(true);
                return completion.Task.WaitAsync(token);
            }
        };
    }

    private static FakeImageDownloadService CreateSuccessfulDownloadService(DownloadedImage image)
    {
        return new FakeImageDownloadService
        {
            OnDownloadAsync = (_, _) => Task.FromResult(image)
        };
    }

    private static FakeImageDownloadService CreateFailingDownloadService(Exception exception)
    {
        return new FakeImageDownloadService
        {
            OnDownloadAsync = (_, _) => Task.FromException<DownloadedImage>(exception)
        };
    }

    private static FakeImageDownloadService CreateFirstPendingSecondSuccessfulDownloadService(
        TaskCompletionSource<bool> firstStarted,
        DownloadedImage secondImage)
    {
        var callCount = 0;

        return new FakeImageDownloadService
        {
            OnDownloadAsync = (_, token) =>
            {
                callCount++;

                if (callCount != 1)
                {
                    return Task.FromResult(secondImage);
                }

                firstStarted.TrySetResult(true);
                var firstCompletion = new TaskCompletionSource<DownloadedImage>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                return firstCompletion.Task.WaitAsync(token);
            }
        };
    }

    private static FakeImageDownloadService CreateUncancellablePendingDownloadService(
        TaskCompletionSource<bool> started,
        TaskCompletionSource<DownloadedImage> completion)
    {
        return new FakeImageDownloadService
        {
            OnDownloadAsync = (_, _) =>
            {
                started.TrySetResult(true);
                return completion.Task;
            }
        };
    }

    private static async Task CompleteDownloadAsync(
        TaskCompletionSource<DownloadedImage> completion,
        Task loadTask)
    {
        completion.TrySetResult(FakeImageDownloadService.CreateDownloadedImage());
        await loadTask;
    }

    private static async Task WaitAsync(Task task, int timeoutMilliseconds = 1000)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMilliseconds));

        if (!ReferenceEquals(completedTask, task))
        {
            Assert.Fail("Operation timed out.");
        }

        await task;
    }

    #endregion

    [TestMethod]
    public async Task LoadCommand_WhenUrlInvalid_DoesNotCallDownload()
    {
        var isDownloadCalled = false;
        var service = new FakeImageDownloadService
        {
            OnDownloadAsync = (_, _) =>
            {
                isDownloadCalled = true;
                return Task.FromResult(FakeImageDownloadService.CreateDownloadedImage());
            }
        };

        using var viewModel = CreateViewModel(service);
        viewModel.Url = InvalidUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.IsFalse(isDownloadCalled);
    }

    [TestMethod]
    public async Task LoadCommand_WhenUrlInvalid_SetsErrorStatus()
    {
        using var viewModel = CreateViewModel();
        viewModel.Url = InvalidUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreEqual("Error", viewModel.StatusText);
    }

    [TestMethod]
    public async Task LoadCommand_WhenDownloadStarts_SetsLoadingStatus()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<DownloadedImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreatePendingDownloadService(started, completion);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        var loadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(started.Task);
        var statusText = viewModel.StatusText;
        await CompleteDownloadAsync(completion, loadTask);

        Assert.AreEqual("Loading...", statusText);
    }

    [TestMethod]
    public async Task LoadCommand_WhenDownloadSucceeds_SetsImage()
    {
        var expectedImage = FakeImageDownloadService.CreateDownloadedImage();
        var service = CreateSuccessfulDownloadService(expectedImage);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreSame(expectedImage.PreviewImage, viewModel.Image);
    }

    [TestMethod]
    public async Task LoadCommand_WhenDownloadSucceeds_SetsOkStatus()
    {
        var service = CreateSuccessfulDownloadService(FakeImageDownloadService.CreateDownloadedImage());

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreEqual("OK", viewModel.StatusText);
    }

    [TestMethod]
    public async Task LoadCommand_WhenServiceFails_SetsErrorStatus()
    {
        var service = CreateFailingDownloadService(new InvalidOperationException("Download failed."));

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreEqual("Error", viewModel.StatusText);
    }

    [TestMethod]
    public async Task CancelCommand_WhenDownloadIsActive_SetsCancelledStatus()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<DownloadedImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreatePendingDownloadService(started, completion);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        var loadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(started.Task);
        viewModel.CancelCommand.Execute(null);
        completion.TrySetCanceled();
        await loadTask;

        Assert.AreEqual("Cancelled", viewModel.StatusText);
    }

    [TestMethod]
    public async Task LoadCommand_WhenCancelledAndStartedAgain_LoadsSecondImage()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedImage = FakeImageDownloadService.CreateDownloadedImage();
        var service = CreateFirstPendingSecondSuccessfulDownloadService(firstStarted, expectedImage);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = FirstImageUrl;

        var firstLoadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(firstStarted.Task);
        viewModel.CancelCommand.Execute(null);
        await firstLoadTask;

        viewModel.Url = SecondImageUrl;
        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreSame(expectedImage.PreviewImage, viewModel.Image);
    }

    [TestMethod]
    public async Task Url_WhenChangedDuringDownload_DoesNotApplyPreviousImage()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<DownloadedImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousImage = FakeImageDownloadService.CreateDownloadedImage();
        var service = CreateUncancellablePendingDownloadService(started, completion);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = FirstImageUrl;

        var loadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(started.Task);
        viewModel.Url = "1";
        completion.TrySetResult(previousImage);
        await loadTask;

        Assert.IsNull(viewModel.Image);
    }

    [TestMethod]
    public async Task LoadCommand_WhenUrlChangedToInvalidDuringDownload_SetsErrorStatus()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<DownloadedImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateUncancellablePendingDownloadService(started, completion);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = FirstImageUrl;

        var loadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(started.Task);
        viewModel.Url = "1";
        completion.TrySetResult(FakeImageDownloadService.CreateDownloadedImage());
        await loadTask;
        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreEqual("Error", viewModel.StatusText);
    }
}
