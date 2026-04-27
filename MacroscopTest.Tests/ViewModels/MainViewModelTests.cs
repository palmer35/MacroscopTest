using MacroscopTest.Services;
using MacroscopTest.Tests.TestHelpers;
using MacroscopTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MacroscopTest.Tests.ViewModels;

[TestClass]
public class MainViewModelTests
{
    #region Helpers

    private const string FirstImageUrl = "https://example.com/first.png";
    private const string SecondImageUrl = "https://example.com/second.png";

    private static (MainViewModel ViewModel, TaskCompletionSource<DownloadedImage> FirstCompletion,
        TaskCompletionSource<DownloadedImage> SecondCompletion) CreateViewModelWithTwoDownloads()
    {
        var firstCompletion =
            new TaskCompletionSource<DownloadedImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion =
            new TaskCompletionSource<DownloadedImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateDownloadService(firstCompletion, secondCompletion);
        var viewModel = CreateViewModel(service);

        viewModel.Slots[0].Url = FirstImageUrl;
        viewModel.Slots[1].Url = SecondImageUrl;

        return (viewModel, firstCompletion, secondCompletion);
    }

    private static FakeImageDownloadService CreateDownloadService(
        TaskCompletionSource<DownloadedImage> firstCompletion,
        TaskCompletionSource<DownloadedImage> secondCompletion)
    {
        var callCount = 0;

        return new FakeImageDownloadService
        {
            OnDownloadWithProgressAsync = (_, token, progress) =>
            {
                callCount++;

                if (callCount == 1)
                {
                    progress?.Report(90);

                    return firstCompletion.Task.WaitAsync(token);
                }

                progress?.Report(0);

                return secondCompletion.Task.WaitAsync(token);
            }
        };
    }

    private static MainViewModel CreateViewModel(FakeImageDownloadService service)
    {
        return new MainViewModel(service, new FileLogger(), 2);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 1000)
    {
        var startedAt = DateTime.UtcNow;

        while (!condition())
        {
            if ((DateTime.UtcNow - startedAt).TotalMilliseconds > timeoutMilliseconds)
            {
                Assert.Fail("Operation timed out.");
            }

            await Task.Delay(10);
        }
    }

    private static async Task CompleteDownloadsAsync(
        MainViewModel viewModel,
        TaskCompletionSource<DownloadedImage> firstCompletion,
        TaskCompletionSource<DownloadedImage> secondCompletion,
        Task firstLoadTask,
        Task secondLoadTask)
    {
        firstCompletion.TrySetResult(FakeImageDownloadService.CreateDownloadedImage());
        secondCompletion.TrySetResult(FakeImageDownloadService.CreateDownloadedImage());

        try
        {
            await Task.WhenAll(firstLoadTask, secondLoadTask);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    #endregion

    [TestMethod]
    public async Task OverallDownloadProgress_WhenSecondDownloadStarts_EqualsAverageProgress()
    {
        const double expectedProgress = 45;
        var (viewModel, firstCompletion, secondCompletion) = CreateViewModelWithTwoDownloads();

        var firstLoadTask = viewModel.Slots[0].LoadCommand.ExecuteAsync();
        await WaitUntilAsync(() => viewModel.OverallDownloadProgress >= 89);
        var secondLoadTask = viewModel.Slots[1].LoadCommand.ExecuteAsync();
        await WaitUntilAsync(() => viewModel.ActiveLoadsCount == 2);
        var actualProgress = viewModel.OverallDownloadProgress;
        await CompleteDownloadsAsync(viewModel, firstCompletion, secondCompletion, firstLoadTask, secondLoadTask);

        Assert.AreEqual(expectedProgress, actualProgress, 1);
    }
}
