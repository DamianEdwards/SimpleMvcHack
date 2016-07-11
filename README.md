#Simplifying MVC for Page-focused Scenarios

We **are** trying to:
- Make getting started rendering dynamic HTML and forms with ASP.NET Core easier, e.g. how many files & concepts required to print Hello World in a page
- Reduce the number of files and folder-structure required for page-focused MVC scenarios
- Simplify the code required to implement common page-focused patterns, e.g. dynamic pages, CRUD forms, PRG, etc.
- Enable the ability to return non-HTML responses when necessary, e.g. 404s
- Use and expose the existing MVC primitives as much as possible
- Allow simple migration to traditional MVC structure
- Do this at a reasonable cost, e.g. 2 developers during 1.1 release

We are **not** trying to:
- Create a scripted page framework to compete with PHP, etc.
- Hide C# with a DSL in Razor or otherwise
- Create new primitives that are only applicable here (i.e. they should be usable in traditionally structured MVC apps too)
- Create new burdens for us WRT to tooling

## CRUD Form Example

Big differences here are the structure of the project and file names, essentially making the controller a code-behind of the view, and using a different controller base class, `ViewController`, that allows us to default some different behavior:
- Instances of `ViewController` are automatically routed based on the controller name, as if `[Route("[controller]")]` were used. Can be overridden by adding the attribute specifically.
- Perhaps we could go all-in on the code-behind model and do the Web Forms approach of generating a .designer.cs file and making the controller a partial class. This would allow us to code-gen more glue between the view and its controller, or just more convenience members for the controller itself, based on the code it already contains.

**Project Structure**
```
Data/
  AppDbContext.cs
  Customer.cs
_Layout.cshtml
Customers.cshtml
Customers.cshtml.cs
Program.cs
project.json
Startup.cs
```

**Customers.cshtml**
```html
@model Customers.ViewModel
@{
    Layout = "_Layout.cshtml";
    // We need to find a nicer way to pass data to the layout page
    ViewData["Title"] = Model.Title;
}

<form method="post" role="form">
    @if (Model.ShowAlertMessage)
    {
        <div class="alert alert-dismissible" role="alert">
            <button type="button" class="close" data-dismiss="alert" aria-label="Close"><span aria-hidden="true">&times;</span></button>
            @Model.AlertMessage
        </div>
    }
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="form-group">
        <label asp-for="Customer.FirstName"></label>
        <input asp-for="Customer.FirstName" class="form-control" />
        <span asp-validation-for="Customer.FirstName" class="help-block text-danger"></span>
    </div>
    <div class="form-group">
        <label asp-for="Customer.LastName"></label>
        <input asp-for="Customer.LastName" class="form-control" />
        <span asp-validation-for="Customer.LastName" class="help-block text-danger"></span>
    </div>
    <div class="form-group">
        <label asp-for="Customer.BirthDate"></label>
        <input asp-for="Customer.BirthDate" class="form-control" />
        <span asp-validation-for="Customer.BirthDate" class="help-block text-danger"></span>
    </div>
    @if (Model.ShowAdultStuff)
    {
    <div class="form-group">
        <label asp-for="Customer.FavoriteDrink"></label>
        <input asp-for="Customer.FavoriteDrink" class="form-control" />
        <span asp-validation-for="Customer.FavoriteDrink" class="help-block text-danger"></span>
    </div>
    }
    <button type="submit" class="btn btn-primary">Save</button>
</form>

@section "Scripts" {
    <partial name="_ValidationScripts" />
}
```

**Customers.cshtml.cs**
```cs
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;

namespace MyApp
{
    public class Customers : ViewController
    {
        public Customers(AppDbContext db)
        {
            Db = db;
        }
    
        public AppDbContext Db { get; }
    
        [HttpGet]
        public IActionResult Get(int? id)
        {
            var customer = id.HasValue ? await Db.Customers.SingleOrDefaultAsync(c => c.Id == id) : (Customer)null;
            return id.HasValue && customer == null ?
                NotFound() :
                View(new ViewModel {
                    Customer = customer,
                    Title = customer != null ? $"Edit Customer {customer?.Id}" : "New Customer"
                });
        }
        
        [HttpPost]
        public IActionResult Post(ViewModel model)
        {
            if (!ModelState.IsValid())
            {
                // Model errors, just return the view to show them the errors
                model.Title = "Edit Customer";
                return View(model);
            }
            
            if (!model.Customer.Id.HasValue)
            {
                // Create
                Db.Customers.Add(model.Customer);
                ViewModel.AlertMessage = $"New customer {model.Customer.Id} created successfully!";
            }
            else
            {
                // Update
                Db.Attach(model.Customer, EntityState.Changed);
                ViewModel.AlertMessage = $"Customer {model.Customer.Id} updated successfully!";
            }
            
            await Db.SaveChangesAsync();
            // TODO: Pfft, errors!? There's no errors :/
        
            // The following line is really verbose, would love to find a way to codify reloading the page in the classic
            // Post -> Redirect -> Get pattern, with type safety based on known action methods on this controller for bonus
            // points, e.g. Reload(model.Customer.Id) or Redirect(() => Get(model.Customer.Id));
            return RedirectToAction(nameof(Get), new { id = model.Customer.Id });
        }
        
        public class ViewModel
        {
          public string Title { get; set; }
          // Would love to have a way to auto-back some properties with TempData via attribute so you don't
          // have to do the code here I have.
          // I think this is the type of the thing the Roslyn team is envisaging code-generators would be
          // be used for but not sure how we'd do it, e.g.
          // [TempData]
          public string AlertMessage {
              get { return TempData[nameof(AlertMessage)]; }
              set { TempData[nameof(AlertMessage)] = value; }
          }
          public bool ShowAlertMessage => !string.IsNullOrEmpty(AlertMessage);
          public Customer Customer { get; set; }
          public bool ShowAdultOnlyThings => (DateTimeOffset.UtcNow - Customer.BirthDate).TotalYears >= 21;
      }
    }
}
```

## CRUD Form Example "Single File"

This differs from the above example by having the view and controller in a single CSHTML file. This has the advantage of reducing the duplication of some of the C# boilerplate usually needed in the standalone CS file, e.g. the `namespace` is generated, the `using` statements can be gathered into the `_ViewImports.cshtml`, etc. We could potentially explore the generated designer/partial file idea too to help make some of the code a little more terse/focused, e.g. the Post -> Redirect -> Get.

This is achieved by:
- Making `ViewController` implement `IView` and `IController`
- Making the view derive from `ViewController`
- Using an `@functions` block in the view to add the action methods and view model
- Profit

**Project Structure**
```
Data/
  AppDbContext.cs
  Customer.cs
_Layout.cshtml
Customers.cshtml
Program.cs
project.json
Startup.cs
```

**Customers.cshtml**
```html
@using MyApp.Data
@inherits ViewController
@model ViewModel
@inject AppDbContext Db
@functions {
    [HttpGet]
    public IActionResult Get(int? id)
    {
        var customer = id.HasValue ? await Db.Customers.SingleOrDefaultAsync(c => c.Id == id) : (Customer)null;
        return id.HasValue && customer == null ?
            NotFound() :
            View(new ViewModel {
                Customer = customer,
                Title = customer != null ? $"Edit Customer {customer?.Id}" : "New Customer"
            });
    }
    
    [HttpPost]
    public IActionResult Post(ViewModel model)
    {
        if (!ModelState.IsValid())
        {
            // Model errors, just return the view to show them the errors
            model.Title = "Edit Customer";
            return View(model);
        }
        
        if (!model.Customer.Id.HasValue)
        {
            // Create
            Db.Customers.Add(model.Customer);
            ViewModel.AlertMessage = $"New customer {model.Customer.Id} created successfully!";
        }
        else
        {
            // Update
            Db.Attach(model.Customer, EntityState.Changed);
            ViewModel.AlertMessage = $"Customer {model.Customer.Id} updated successfully!";
        }
        
        await Db.SaveChangesAsync();
        // TODO: Pfft, errors!? There's no errors :/
    
        // The following line is really verbose, would love to find a way to codify reloading the page in the classic
        // Post -> Redirect -> Get pattern, with type safety based on known action methods on this controller for bonus
        // points, e.g. Reload(model.Customer.Id) or Redirect(() => Get(model.Customer.Id));
        return RedirectToAction(nameof(Get), new { id = model.Customer.Id });
    }
    
    public class ViewModel
    {
        public string Title { get; set; }
        // Would love to have a way to auto-back some properties with TempData via attribute so you don't
        // have to do the code here I have.
        // I think this is the type of the thing the Roslyn team is envisaging code-generators would be
        // be used for but not sure how we'd do it, e.g.
        // [TempData]
        public string AlertMessage {
          get { return TempData[nameof(AlertMessage)]; }
          set { TempData[nameof(AlertMessage)] = value; }
        }
        public bool ShowAlertMessage => !string.IsNullOrEmpty(AlertMessage);
        public Customer Customer { get; set; }
        public bool ShowAdultOnlyThings => (DateTimeOffset.UtcNow - Customer.BirthDate).TotalYears >= 21;
    }
}
@{
    Layout = "_Layout.cshtml";
    // We need to find a nicer way to pass data to the layout page
    ViewData["Title"] = Model.Title;
}

<form method="post" role="form">
    @if (Model.ShowAlertMessage)
    {
        <div class="alert alert-dismissible" role="alert">
            <button type="button" class="close" data-dismiss="alert" aria-label="Close"><span aria-hidden="true">&times;</span></button>
            @Model.AlertMessage
        </div>
    }
    <div asp-validation-summary="All" class="text-danger"></div>
    <div class="form-group">
        <label asp-for="Customer.FirstName"></label>
        <input asp-for="Customer.FirstName" class="form-control" />
        <span asp-validation-for="Customer.FirstName" class="help-block text-danger"></span>
    </div>
    <div class="form-group">
        <label asp-for="Customer.LastName"></label>
        <input asp-for="Customer.LastName" class="form-control" />
        <span asp-validation-for="Customer.LastName" class="help-block text-danger"></span>
    </div>
    <div class="form-group">
        <label asp-for="Customer.BirthDate"></label>
        <input asp-for="Customer.BirthDate" class="form-control" />
        <span asp-validation-for="Customer.BirthDate" class="help-block text-danger"></span>
    </div>
    @if (Model.ShowAdultStuff)
    {
    <div class="form-group">
        <label asp-for="Customer.FavoriteDrink"></label>
        <input asp-for="Customer.FavoriteDrink" class="form-control" />
        <span asp-validation-for="Customer.FavoriteDrink" class="help-block text-danger"></span>
    </div>
    }
    <button type="submit" class="btn btn-primary">Save</button>
</form>

@section "Scripts" {
    <partial name="_ValidationScripts" />
}
```