using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CSVUpload.Models;
using CSVUpload.DBContext;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection.Metadata;
using System.Xml.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace CSVUpload.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DataContext _db;

    public HomeController(ILogger<HomeController> logger, DataContext db)
    {
        _logger = logger;
        _db = db;
    }




    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users); // pass users to view
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // Create User (POST)
    [HttpPost]
    public async Task<IActionResult> Create(User user)
    {
        if (ModelState.IsValid)
        {
            user.CreatedAt = DateTime.UtcNow;
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            TempData["Success"] = "User created successfully!";
            return RedirectToAction(nameof(Index));
        }
        return View(user);
    }

    // Edit User (GET)
    // GET: Home/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        return View(user);
    }

    // POST: Home/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(User user)
    {
        if (!ModelState.IsValid)
        {
            return View(user);
        }

        var existingUser = await _db.Users.FindAsync(user.Id);
        if (existingUser == null)
        {
            return NotFound();
        }

        // Update fields
        existingUser.Name = user.Name;
        existingUser.Email = user.Email;
        existingUser.Address = user.Address;
        existingUser.About = user.About;
        existingUser.Number = user.Number;

        await _db.SaveChangesAsync();

        TempData["Success"] = "User updated successfully.";
        return RedirectToAction("Index");
    }


    // Delete User
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        TempData["Success"] = "User deleted successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReport()
    {
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();

        using (var ms = new MemoryStream())
        {
            // Step 1: Create PDF document
            iTextSharp.text.Document document = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
            PdfWriter.GetInstance(document, ms);
            document.Open();

            // Step 2: Add Title
            var titleFont = FontFactory.GetFont("Arial", 16, iTextSharp.text.Font.BOLD);
            Paragraph title = new Paragraph("Uploaded Users Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // Step 3: Create table
            PdfPTable table = new PdfPTable(7); // 7 columns
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 3f, 10f, 15f, 15f, 15f, 10f, 15f });

            // Add header
            var headerFont = FontFactory.GetFont("Arial", 12, iTextSharp.text.Font.BOLD);
            string[] headers = { "#", "Name", "Email", "Address", "About", "Number", "Created At" };
            foreach (var h in headers)
            {
                PdfPCell cell = new PdfPCell(new Phrase(h, headerFont));
                cell.BackgroundColor = BaseColor.LIGHT_GRAY;
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                cell.Padding = 5;
                table.AddCell(cell);
            }

            // Add data rows
            int i = 1;
            var rowFont = FontFactory.GetFont("Arial", 11, iTextSharp.text.Font.NORMAL);
            foreach (var user in users)
            {
                table.AddCell(new PdfPCell(new Phrase(i.ToString(), rowFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(user.Name ?? "", rowFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(user.Email ?? "", rowFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(user.Address ?? "", rowFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(user.About ?? "", rowFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(user.Number ?? "", rowFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), rowFont)) { Padding = 5 });
                i++;
            }

            document.Add(table);
            document.Close();

            var bytes = ms.ToArray();
            return File(bytes, "application/pdf", "UploadedUsersReport.pdf");
        }
    }

    [HttpGet]
    public IActionResult About()
    {
        return View();
    }


    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Upload()
    {
        // Load all users from DB
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users); // pass users to view
    }


    [HttpPost]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a CSV file.";
            return RedirectToAction(nameof(Upload));
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only CSV files are allowed.";
            return RedirectToAction(nameof(Upload));
        }

        var usersToAdd = new List<User>();

        using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        }))
        {
            var records = csv.GetRecords<dynamic>();
            foreach (var rec in records)
            {
                var dict = rec as IDictionary<string, object?>;

                string name = dict.TryGetValue("Name", out var val) ? val?.ToString()?.Trim() ?? "" : "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                var user = new User
                {
                    Name = name,
                    Email = dict.TryGetValue("Email", out var e) ? e?.ToString()?.Trim() : null,
                    Address = dict.TryGetValue("Address", out var a) ? a?.ToString()?.Trim() : null,
                    About = dict.TryGetValue("About", out var ab) ? ab?.ToString()?.Trim() : null,
                    Number = dict.TryGetValue("Number", out var n) ? n?.ToString()?.Trim() : null,
                    CreatedAt = DateTime.UtcNow
                };

                usersToAdd.Add(user);
            }
        }

        if (!usersToAdd.Any())
        {
            TempData["Warning"] = "No valid user rows found in CSV.";
            return RedirectToAction(nameof(Upload));
        }

        await _db.Users.AddRangeAsync(usersToAdd, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"{usersToAdd.Count} user(s) imported successfully.";
        return RedirectToAction(nameof(Upload));
    }

    [HttpGet]
    public FileResult DownloadTemplate()
    {
        var csv = "Name,Email,Address,About,Number\r\nRahim Uddin,rahim@example.com,Dhaka,Developer,01710000001\r\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "users-template.csv");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
