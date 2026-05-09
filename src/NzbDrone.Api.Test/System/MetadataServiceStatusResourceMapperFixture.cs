using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Update;
using Readarr.Api.V1.System;

namespace NzbDrone.Api.Test.System
{
    [TestFixture]
    public class MetadataServiceStatusResourceMapperFixture
    {
        [Test]
        public void should_report_local_rreading_glasses_as_reachable()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource(MetadataSourceConfig.LocalRReadingGlasses,
                new MetadataSourceHealthResult
                {
                    IsHealthy = true,
                    Message = "Metadata source is reachable",
                    Detail = "Author lookup completed in 25 ms",
                    StatusCode = 200,
                    ResponseTimeMs = 25
                },
                null,
                "No ReadAIrr app update is currently available.",
                true,
                "dev",
                new Version(1, 0, 0));

            resource.SourceType.Should().Be("localRReadingGlasses");
            resource.ReadinessState.Should().Be("reachable");
            resource.IsReachable.Should().BeTrue();
            resource.ServiceUrl.Should().Be(MetadataSourceConfig.LocalRReadingGlasses);
            resource.UpdateEndpoint.Should().Be("https://readairr.com/v1/update/{branch}");
            resource.UpdateAvailable.Should().BeFalse();
            resource.SidecarManagementMode.Should().Be("readarrDeploymentSidecar");
            resource.SidecarManagedByReadAIrr.Should().BeTrue();
            resource.SidecarUpdateSupported.Should().BeFalse();
            resource.SidecarUpdateAction.Should().Be("manualDockerImageUpdate");
            resource.SidecarUpdateAvailable.Should().BeNull();
            resource.SidecarVersionMessage.Should().Contain("does not expose version metadata");
            resource.SidecarUpdateGuidance.Should().Contain("Docker deployments");
            resource.AutomaticMetadataDecisioningEnabled.Should().BeFalse();
            resource.ConfidenceMode.Should().Be("sourceRolesOnly");
            resource.ConfidenceSignals.Should().ContainSingle(x => x.SourceType == "localRReadingGlasses" && x.Role == "primary" && x.IsActive && x.ConfidenceWeight == 100);
            resource.ConfidenceSignals.Should().ContainSingle(x => x.SourceType == "aiReview" && x.Status == "disabled");
            resource.Warnings.Should().BeEmpty();
        }

        [Test]
        public void should_warn_for_original_readarr_compatibility_source()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource(MetadataSourceConfig.OriginalReadarr,
                new MetadataSourceHealthResult
                {
                    IsHealthy = true,
                    Message = "Metadata source is reachable"
                },
                null,
                "No ReadAIrr app update is currently available.",
                true,
                "dev",
                new Version(1, 0, 0));

            resource.SourceType.Should().Be("originalReadarr");
            resource.SourceLabel.Should().Contain("Original Readarr");
            resource.SidecarManagementMode.Should().Be("legacyCompatibility");
            resource.SidecarManagedByReadAIrr.Should().BeFalse();
            resource.SidecarUpdateAction.Should().Be("switchMetadataSource");
            resource.SidecarUpdateCheckMessage.Should().Contain("No sidecar update check applies");
            resource.ConfidenceSignals.Should().ContainSingle(x => x.SourceType == "originalReadarr" && x.Role == "primary" && x.IsActive);
            resource.Warnings.Should().Contain(x => x.Contains("legacy compatibility"));
        }

        [Test]
        public void should_keep_combined_confidence_stub_inert_when_primary_source_is_unreachable()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource(MetadataSourceConfig.HardcoverHosted,
                new MetadataSourceHealthResult
                {
                    IsHealthy = false,
                    Message = "Metadata source returned HTTP 503",
                    StatusCode = 503
                },
                null,
                "No ReadAIrr app update is currently available.",
                true,
                "dev",
                new Version(1, 0, 0));

            resource.AutomaticMetadataDecisioningEnabled.Should().BeFalse();
            resource.ConfidenceSummary.Should().Contain("matching/import behavior is unchanged");
            resource.ConfidenceSignals.Should().ContainSingle(x => x.SourceType == "hostedHardcover" &&
                                                                   x.Role == "primary" &&
                                                                   x.IsActive &&
                                                                   !x.IsAvailable &&
                                                                   x.ConfidenceWeight == 0 &&
                                                                   x.Status == "unreachable");
            resource.ConfidenceSignals.Should().Contain(x => x.SourceType == "hostedGoodreads" && x.Status == "notEvaluated");
            resource.ConfidenceSignals.Should().Contain(x => x.SourceType == "aiReview" && x.Status == "disabled");
        }

        [Test]
        public void should_redact_custom_metadata_url_userinfo_and_query()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource("https://user:secret@metadata.example.test:8443/v1?token=abc",
                new MetadataSourceHealthResult
                {
                    IsHealthy = false,
                    Message = "Metadata source returned HTTP 401",
                    Detail = "Unauthorized",
                    StatusCode = 401
                },
                null,
                "No ReadAIrr app update is currently available.",
                true,
                "dev",
                new Version(1, 0, 0));

            resource.SourceType.Should().Be("custom");
            resource.ServiceUrl.Should().Contain("redacted@metadata.example.test:8443");
            resource.ServiceUrl.Should().NotContain("secret");
            resource.ServiceUrl.Should().NotContain("token");
            resource.SidecarManagementMode.Should().Be("externalCustom");
            resource.SidecarUpdateSupported.Should().BeFalse();
            resource.SidecarUpdateGuidance.Should().Contain("their own deployment process");
            resource.Warnings.Should().Contain("Metadata source returned HTTP 401");
        }

        [Test]
        public void should_report_hosted_rreading_glasses_as_externally_managed()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource(MetadataSourceConfig.HardcoverHosted,
                new MetadataSourceHealthResult
                {
                    IsHealthy = true,
                    Message = "Metadata source is reachable"
                },
                null,
                "No ReadAIrr app update is currently available.",
                true,
                "dev",
                new Version(1, 0, 0));

            resource.SourceType.Should().Be("hostedHardcover");
            resource.SidecarManagementMode.Should().Be("externalHosted");
            resource.SidecarManagementLabel.Should().Be("Externally managed hosted service");
            resource.SidecarManagedByReadAIrr.Should().BeFalse();
            resource.SidecarUpdateSupported.Should().BeFalse();
            resource.SidecarUpdateAction.Should().Be("managedExternally");
            resource.SidecarUpdateCheckMessage.Should().Contain("managed outside ReadAIrr");
            resource.SidecarVersionMessage.Should().Contain("Hosted rreading-glasses services");
        }

        [Test]
        public void should_report_available_readairr_update()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource(MetadataSourceConfig.GoodreadsHosted,
                new MetadataSourceHealthResult
                {
                    IsHealthy = true,
                    Message = "Metadata source is reachable"
                },
                new UpdatePackage
                {
                    Version = new Version(1, 2, 0),
                    Branch = "dev",
                    ReleaseDate = new DateTime(2026, 5, 9)
                },
                "ReadAIrr update metadata checked successfully.",
                true,
                "dev",
                new Version(1, 1, 0));

            resource.UpdateAvailable.Should().BeTrue();
            resource.LatestVersion.Should().Be("1.2.0");
            resource.Warnings.Should().Contain(x => x.Contains("ReadAIrr update 1.2.0 is available"));
        }

        [Test]
        public void should_report_unknown_update_availability_when_update_check_fails()
        {
            var resource = MetadataServiceStatusResourceMapper.ToResource(MetadataSourceConfig.LocalRReadingGlasses,
                new MetadataSourceHealthResult
                {
                    IsHealthy = true,
                    Message = "Metadata source is reachable"
                },
                null,
                "ReadAIrr update metadata check failed: HttpException",
                false,
                "dev",
                new Version(1, 0, 0));

            resource.UpdateAvailable.Should().BeNull();
            resource.UpdateCheckMessage.Should().Be("ReadAIrr update metadata check failed: HttpException");
        }
    }
}
