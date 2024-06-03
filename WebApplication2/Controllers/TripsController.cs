// TripsController.cs
using cw09.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using cw09.Models;

namespace cw09.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController : ControllerBase
{
    private readonly ApbdContext _context;

    public TripsController(ApbdContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrips([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var totalTrips = await _context.Trips.CountAsync();
        var trips = await _context.Trips
            .Include(e => e.IdCountries)
            .Include(e => e.ClientTrips)
            .OrderByDescending(e => e.DateFrom)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                Name = e.Name,
                Description = e.Description,
                DateFrom = e.DateFrom,
                DateTo = e.DateTo,
                MaxPeople = e.MaxPeople,
                Countries = e.IdCountries.Select(c => new
                {
                    Name = c.Name
                }),
                Clients = e.ClientTrips.Select(ct => new
                {
                    FirstName = ct.IdClientNavigation.FirstName,
                    LastName = ct.IdClientNavigation.LastName
                })
            })
            .ToListAsync();

        return Ok(new
        {
            pageNum = page,
            pageSize,
            allPages = (int)Math.Ceiling((double)totalTrips / pageSize),
            trips
        });
    }

    [HttpDelete("clients/{idClient}")]
    public async Task<IActionResult> DeleteClient(int idClient)
    {
        var client = await _context.Clients
            .Include(c => c.ClientTrips)
            .FirstOrDefaultAsync(c => c.IdClient == idClient);

        if (client == null)
        {
            return NotFound("Client not found");
        }

        if (client.ClientTrips.Any())
        {
            return BadRequest("Cannot delete client with assigned trips");
        }

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("trips/{idTrip}/clients")]
    public async Task<IActionResult> AssignClientToTrip(int idTrip, [FromBody] ClientTrip clientTrip)
    {
        var trip = await _context.Trips.FindAsync(idTrip);
        if (trip == null || trip.DateFrom <= DateTime.Now)
        {
            return BadRequest("Trip does not exist or has already started");
        }

        var client = await _context.Clients
            .Include(c => c.ClientTrips)
            .FirstOrDefaultAsync(c => c.Pesel == clientTrip.IdClientNavigation.Pesel);

        if (client != null && client.ClientTrips.Any(ct => ct.IdTrip == idTrip))
        {
            return BadRequest("Client is already registered for this trip");
        }

        if (client == null)
        {
            client = new Client
            {
                FirstName = clientTrip.IdClientNavigation.FirstName,
                LastName = clientTrip.IdClientNavigation.LastName,
                Email = clientTrip.IdClientNavigation.Email,
                Telephone = clientTrip.IdClientNavigation.Telephone,
                Pesel = clientTrip.IdClientNavigation.Pesel
            };
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();
        }

        var newClientTrip = new ClientTrip
        {
            IdClient = client.IdClient,
            IdTrip = idTrip,
            RegisteredAt = DateTime.Now,
            PaymentDate = clientTrip.PaymentDate
        };

        _context.ClientTrips.Add(newClientTrip);
        await _context.SaveChangesAsync();

        return Ok(newClientTrip);
    }
}
