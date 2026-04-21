using System.Windows.Media.Imaging;
using MacroscopTest.Services;
using MacroscopTest.Tests.TestHelpers;
using MacroscopTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MacroscopTest.Tests.ViewModels;

[TestClass]
public sealed class ImageSlotViewModelTests
{
    #region TestHelpers

    private const string DefaultImageUrl = "https://example.com/image.png";
    private const string FirstImageUrl = "https://example.com/first.png";
    private const string SecondImageUrl = "https://example.com/second.png";

    private static ImageSlotViewModel CreateViewModel(FakeImageDownloadService? service = null)
    {
        service ??= new FakeImageDownloadService();

        return new ImageSlotViewModel(service, new FileLogger());
    }

    private static FakeImageDownloadService CreatePendingDownloadService(
        TaskCompletionSource<bool> started,
        TaskCompletionSource<BitmapImage> completion)
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

    private static FakeImageDownloadService CreateSuccessfulDownloadService(BitmapImage image)
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
            OnDownloadAsync = (_, _) => Task.FromException<BitmapImage>(exception)
        };
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

    #endregion TestHelpers

    #region Tests

    [TestMethod]
    public async Task LoadAsync_WithInvalidUrl_SetsErrorState()
    {
        var isDownloadCalled = false;
        var service = new FakeImageDownloadService
        {
            OnDownloadAsync = (_, _) =>
            {
                isDownloadCalled = true;
                return Task.FromResult(FakeImageDownloadService.CreateImage());
            }
        };

        using var viewModel = CreateViewModel(service);
        viewModel.Url = "invalid-url";

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.IsFalse(isDownloadCalled);
        Assert.IsFalse(viewModel.IsLoading);
        Assert.IsNull(viewModel.Image);
        Assert.AreEqual("Error", viewModel.StatusText);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ErrorText));
    }

    [TestMethod]
    public async Task LoadAsync_WhenDownloadStarts_SetsLoadingState()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<BitmapImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreatePendingDownloadService(started, completion);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        var loadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(started.Task);

        try
        {
            Assert.IsTrue(viewModel.IsLoading);
            Assert.IsNull(viewModel.Image);
            Assert.IsNull(viewModel.ErrorText);
            Assert.AreEqual("Loading...", viewModel.StatusText);
        }
        finally
        {
            completion.TrySetResult(FakeImageDownloadService.CreateImage());
            await loadTask;
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenDownloadSucceeds_SetsLoadedState()
    {
        var expectedImage = FakeImageDownloadService.CreateImage();
        var service = CreateSuccessfulDownloadService(expectedImage);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreSame(expectedImage, viewModel.Image);
        Assert.AreEqual("Loaded", viewModel.StatusText);
        Assert.IsNull(viewModel.ErrorText);
        Assert.IsFalse(viewModel.IsLoading);
    }

    [TestMethod]
    public async Task LoadAsync_WhenServiceFails_SetsErrorState()
    {
        var service = CreateFailingDownloadService(new InvalidOperationException("Download failed."));

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.IsFalse(viewModel.IsLoading);
        Assert.IsNull(viewModel.Image);
        Assert.AreEqual("Error", viewModel.StatusText);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ErrorText));
    }

    [TestMethod]
    public async Task LoadAsync_WhenCancelled_SetsCancelledState()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<BitmapImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreatePendingDownloadService(started, completion);

        using var viewModel = CreateViewModel(service);
        viewModel.Url = DefaultImageUrl;

        var loadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(started.Task);
        viewModel.CancelCommand.Execute(null);
        completion.TrySetCanceled();
        await loadTask;

        Assert.IsFalse(viewModel.IsLoading);
        Assert.IsNull(viewModel.Image);
        Assert.AreEqual("Cancelled", viewModel.StatusText);
    }

    [TestMethod]
    public async Task LoadAsync_AfterCancellation_LoadsImageForNewUrl()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var expectedImage = FakeImageDownloadService.CreateImage();
        var service = new FakeImageDownloadService
        {
            OnDownloadAsync = (_, token) =>
            {
                callCount++;

                if (callCount != 1)
                {
                    return Task.FromResult(expectedImage);
                }

                firstStarted.TrySetResult(true);
                var firstCompletion = new TaskCompletionSource<BitmapImage>(TaskCreationOptions.RunContinuationsAsynchronously);

                return firstCompletion.Task.WaitAsync(token);

            }
        };

        using var viewModel = CreateViewModel(service);
        viewModel.Url = FirstImageUrl;

        var firstLoadTask = viewModel.LoadCommand.ExecuteAsync();
        await WaitAsync(firstStarted.Task);
        viewModel.CancelCommand.Execute(null);
        await firstLoadTask;

        viewModel.Url = SecondImageUrl;
        await viewModel.LoadCommand.ExecuteAsync();

        Assert.AreSame(expectedImage, viewModel.Image);
        Assert.AreEqual("Loaded", viewModel.StatusText);
        Assert.IsNull(viewModel.ErrorText);
        Assert.IsFalse(viewModel.IsLoading);
    }

    #endregion Tests
}
