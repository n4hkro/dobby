using Dobby.Application.Printing;
using Dobby.Host;
using Dobby.Infrastructure.Printing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class WindowsPrintTargetClassifierAdapterTests
{
    [Fact]
    public void Adapter_maps_physical_printer_result_to_application_contract()
    {
        var adapter = new WindowsPrintTargetClassifierAdapter(new WindowsPrintTargetClassifier());

        var result = adapter.Classify(new PrintTargetDescriptor(
            "Office Laser",
            "Vendor Driver",
            "USB001",
            IsLocalPhysicalPrinter: true));

        result.ShouldBe(PrintTargetClass.PhysicalPrinter);
    }

    [Theory]
    [InlineData("Microsoft Print to PDF", "PORTPROMPT:")]
    [InlineData("XPS Document Writer", "FILE:")]
    public void Adapter_maps_virtual_or_file_output_to_application_contract(string driverName, string portName)
    {
        var adapter = new WindowsPrintTargetClassifierAdapter(new WindowsPrintTargetClassifier());

        var result = adapter.Classify(new PrintTargetDescriptor(
            "Virtual Output",
            driverName,
            portName,
            IsLocalPhysicalPrinter: false));

        result.ShouldBe(PrintTargetClass.VirtualOrFileOutput);
    }
}
