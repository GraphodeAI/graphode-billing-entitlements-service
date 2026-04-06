using Graphode.BillingEntitlementsService.Contracts.ContractArtifacts;
using FluentAssertions;

namespace Graphode.BillingEntitlementsService.Tests;

public sealed class ContractArtifactCatalogTests
{
    [Fact]
    public void Contract_catalog_contains_required_machine_readable_outputs()
    {
        var fileNames = ContractArtifactCatalog.Definitions.Select(definition => definition.FileName).ToArray();

        fileNames.Should().Contain("read.reference-items.request.schema.json");
        fileNames.Should().Contain("read.reference-items.response.schema.json");
        fileNames.Should().Contain("command.reference-items.archive.envelope.schema.json");
        fileNames.Should().Contain("event.reference-items.created.envelope.schema.json");
        fileNames.Should().Contain("pem.reference-items.platform-event-model.envelope.schema.json");
        fileNames.Should().OnlyHaveUniqueItems();
    }
}
