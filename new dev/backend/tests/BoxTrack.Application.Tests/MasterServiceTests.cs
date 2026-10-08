using BoxTrack.Application;

namespace BoxTrack.Application.Tests;

public sealed class MasterServiceTests
{
    [Fact]
    public void Item_codes_start_at_101_and_increment()
    {
        var service = new MasterService();
        var department = service.Departments(null, 1, 10).Items[0];
        var first = service.CreateItem(new ItemInput("Sample", null, 10, 1.25m, department.Id));
        var second = service.CreateItem(new ItemInput("Sample 2", null, 10, 1.25m, department.Id));
        Assert.Equal(101, first.Code);
        Assert.Equal(102, second.Code);
    }

    [Fact]
    public void Customer_import_values_are_normalized_and_pincode_validated()
    {
        var service = new MasterService();
        var customer = service.CreateCustomer(new CustomerInput(77, "Sample Customer", null, null, "Vapi", "396191", "GJ", "IN"));
        Assert.Equal("Gujarat", customer.State);
        Assert.Equal("India", customer.Country);
        Assert.Throws<InvalidOperationException>(() => service.CreateCustomer(new CustomerInput(null, "Bad", null, null, null, "123", null, null)));
    }

    [Fact]
    public void Department_with_items_cannot_be_deleted()
    {
        var service = new MasterService();
        var department = service.Departments(null, 1, 10).Items[0];
        service.CreateItem(new ItemInput("Sample", null, 1, 1, department.Id));
        var exception = Assert.Throws<InvalidOperationException>(() => service.DeleteDepartment(department.Id));
        Assert.Contains("items exist", exception.Message);
    }
}
