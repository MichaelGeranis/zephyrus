using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.UnitTests.UseCases;

public class RerunStepUseCaseTests
{
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly RerunStepUseCase _sut;

    public RerunStepUseCaseTests()
    {
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
        _sut = new RerunStepUseCase(_featureRepo, _serviceProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(Guid.NewGuid()));
    }

    private static Feature CreateFeatureAt(Guid projectId, FeatureStatus targetStatus)
    {
        var feature = Feature.Create(projectId, "prompt");
        while (feature.Status != targetStatus)
            feature.Advance();
        return feature;
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownStep_ThrowsInvalidOperationException()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(feature.Id, "unknownstep"));
    }

    [Theory]
    [InlineData(FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.PrdApproved)]
    [InlineData(FeatureStatus.ArchApproved)]
    [InlineData(FeatureStatus.TasksApproved)]
    [InlineData(FeatureStatus.Deployed)]
    public async Task ExecuteAsync_WhenStatusHasNoRerunStep_ThrowsInvalidOperationException(FeatureStatus status)
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), status);
        await _featureRepo.AddAsync(feature);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(feature.Id));
    }
}
