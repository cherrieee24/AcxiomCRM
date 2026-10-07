using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

/// <summary>
/// Applies migrations, creates the three roles, and (when configured) demo users and sample CRM data.
/// Demo credentials come from configuration ("Seed:Users"), never from source code.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var seedUsers = config.GetSection("Seed:Users").Get<List<SeedUser>>() ?? new();
        var created = new Dictionary<string, ApplicationUser>();
        foreach (var su in seedUsers)
        {
            var user = await userManager.FindByEmailAsync(su.Email);
            if (user == null)
            {
                user = new ApplicationUser { UserName = su.Email, Email = su.Email, FullName = su.FullName, EmailConfirmed = true };
                if (su.ManagerEmail != null && created.TryGetValue(su.ManagerEmail, out var mgr)) user.ManagerId = mgr.Id;
                var result = await userManager.CreateAsync(user, su.Password);
                if (!result.Succeeded)
                {
                    logger.LogWarning("Could not seed user {Email}: {Errors}", su.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }
                await userManager.AddToRoleAsync(user, su.Role);
            }
            created[su.Email] = user;
        }

        if (config.GetValue<bool>("Seed:SampleData") && !await db.Customers.IgnoreQueryFilters().AnyAsync())
        {
            var sales = new List<ApplicationUser>();
            foreach (var su in seedUsers.Where(u => u.Role == Roles.SalesExecutive))
                if (created.TryGetValue(su.Email, out var u)) sales.Add(u);
            if (sales.Count > 0) await SeedSampleDataAsync(db, sales);
        }
    }

    private static async Task SeedSampleDataAsync(ApplicationDbContext db, List<ApplicationUser> sales)
    {
        var rnd = new Random(42);
        var today = DateTime.Today;
        string Owner(int i) => sales[i % sales.Count].Id;

        var companies = new[]
        {
            ("Aarav Sharma", "Infosys Ltd", "Bengaluru", "Karnataka"),
            ("Priya Nair", "Tata Consultancy", "Mumbai", "Maharashtra"),
            ("Rohan Mehta", "Reliance Retail", "Mumbai", "Maharashtra"),
            ("Ananya Iyer", "Zoho Corp", "Chennai", "Tamil Nadu"),
            ("Vikram Reddy", "Cyient", "Hyderabad", "Telangana"),
            ("Sneha Kapoor", "HCL Technologies", "Noida", "Uttar Pradesh"),
            ("Arjun Verma", "Mahindra Logistics", "Pune", "Maharashtra"),
            ("Kavya Rao", "Freshworks", "Chennai", "Tamil Nadu"),
            ("Ishaan Gupta", "Paytm", "Noida", "Uttar Pradesh"),
            ("Meera Joshi", "Wipro", "Bengaluru", "Karnataka"),
            ("Aditya Singh", "Swiggy", "Bengaluru", "Karnataka"),
            ("Diya Patel", "Adani Ports", "Ahmedabad", "Gujarat")
        };

        var customers = companies.Select((c, i) => new Customer
        {
            CustomerCode = $"CUS-{i + 1:D5}",
            CustomerName = c.Item1,
            CompanyName = c.Item2,
            Email = $"{c.Item1.Split(' ')[0].ToLower()}.{c.Item1.Split(' ')[1].ToLower()}@example.com",
            Phone = $"9{rnd.Next(100000000, 999999999)}",
            City = c.Item3,
            State = c.Item4,
            Address = $"{rnd.Next(1, 300)}, MG Road",
            Status = i % 7 == 6 ? CustomerStatus.Inactive : CustomerStatus.Active,
            AssignedTo = Owner(i)
        }).ToList();
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        var leadNames = new[]
        {
            ("Nikhil Bansal", "Ola Electric"), ("Pooja Desai", "Nykaa"), ("Rahul Khanna", "Zomato"),
            ("Tanvi Shah", "CRED"), ("Karan Malhotra", "Byju's"), ("Riya Sen", "Lenskart"),
            ("Siddharth Jain", "PhonePe"), ("Neha Kulkarni", "Dream11"), ("Manish Tiwari", "Meesho"),
            ("Shreya Ghosh", "Razorpay"), ("Varun Chopra", "Urban Company"), ("Aisha Khan", "Delhivery"),
            ("Harsh Agarwal", "BigBasket"), ("Nisha Pillai", "InMobi")
        };
        var statuses = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost, LeadStatus.New, LeadStatus.Contacted };
        var leads = leadNames.Select((l, i) => new Lead
        {
            LeadCode = $"LD-{i + 1:D5}",
            LeadName = l.Item1,
            CompanyName = l.Item2,
            Email = $"{l.Item1.Split(' ')[0].ToLower()}@{l.Item2.ToLower().Replace(" ", "").Replace("'", "")}.example.com",
            Phone = $"8{rnd.Next(100000000, 999999999)}",
            Source = (LeadSource)(i % Enum.GetValues<LeadSource>().Length),
            Status = statuses[i % statuses.Length],
            Priority = (Priority)(i % 3),
            ExpectedValue = rnd.Next(5, 80) * 10000,
            AssignedTo = Owner(i)
        }).ToList();
        leads[^1].Status = LeadStatus.Converted;
        leads[^1].ConvertedCustomerId = customers[0].CustomerId;
        leads[^1].ConvertedDate = DateTime.UtcNow.AddDays(-20);
        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();

        var opps = new List<Opportunity>();
        var stages = Enum.GetValues<OpportunityStage>();
        for (var i = 0; i < 24; i++)
        {
            var customer = customers[i % customers.Count];
            var stage = stages[i % stages.Length];
            var status = Opportunity.StatusFor(stage);
            var closed = status != OpportunityStatus.Open;
            var closedOn = DateTime.UtcNow.AddMonths(-(i % 11)).AddDays(-rnd.Next(0, 20));
            opps.Add(new Opportunity
            {
                OpportunityName = $"{customer.CompanyName} - {(i % 3 == 0 ? "Data Platform" : i % 3 == 1 ? "Marketing Cloud" : "Identity Resolution")}",
                CustomerId = customer.CustomerId,
                Amount = rnd.Next(2, 60) * 25000,
                Stage = stage,
                Status = status,
                Probability = stage switch
                {
                    OpportunityStage.Qualification => 20,
                    OpportunityStage.Proposal => 50,
                    OpportunityStage.Negotiation => 75,
                    OpportunityStage.Won => 100,
                    _ => 0
                },
                ExpectedCloseDate = closed ? closedOn.Date : today.AddDays(rnd.Next(7, 120)),
                ClosedDate = closed ? closedOn : null,
                AssignedTo = customer.AssignedTo
            });
        }
        db.Opportunities.AddRange(opps);
        await db.SaveChangesAsync();

        var followUps = new List<FollowUp>();
        for (var i = 0; i < 16; i++)
        {
            var customer = customers[i % customers.Count];
            var offset = i - 5; // a few overdue, most upcoming
            followUps.Add(new FollowUp
            {
                Subject = i % 2 == 0 ? $"Proposal review with {customer.CustomerName}" : $"Check-in call - {customer.CompanyName}",
                FollowUpType = (FollowUpType)(i % 4),
                FollowUpDate = today.AddDays(offset * 2),
                Status = i < 3 ? FollowUpStatus.Completed : FollowUpStatus.Planned,
                CompletedDate = i < 3 ? DateTime.UtcNow.AddDays(offset * 2) : null,
                CustomerId = customer.CustomerId,
                AssignedTo = customer.AssignedTo
            });
        }
        db.FollowUps.AddRange(followUps);

        var activities = new List<Activity>();
        for (var i = 0; i < 14; i++)
        {
            var customer = customers[i % customers.Count];
            activities.Add(new Activity
            {
                ActivityType = (ActivityType)(i % 4),
                Subject = ((ActivityType)(i % 4)) switch
                {
                    ActivityType.Call => $"Discovery call with {customer.CustomerName}",
                    ActivityType.Meeting => $"On-site meeting at {customer.CompanyName}",
                    ActivityType.Email => "Sent pricing deck",
                    _ => "Prepare renewal quote"
                },
                ActivityDate = today.AddDays(-rnd.Next(0, 30)),
                Status = i % 5 == 0 ? ActivityStatus.Planned : ActivityStatus.Completed,
                CustomerId = customer.CustomerId,
                AssignedTo = customer.AssignedTo
            });
        }
        db.Activities.AddRange(activities);
        await db.SaveChangesAsync();
    }

    private sealed class SeedUser
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = Roles.SalesExecutive;
        public string? ManagerEmail { get; set; }
    }
}
