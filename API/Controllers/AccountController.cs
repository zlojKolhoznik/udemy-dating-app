using System.Security.Cryptography;
using System.Text;
using API.Data;
using API.DTO;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

public class AccountController : BaseApiController
{
    private readonly DataContext _context;
    private readonly ITokenService _tokenService;

    public AccountController(DataContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost("register")] // POST: api/account/register
    public async Task<ActionResult<UserDto>> Register(RegisterDto dto)
    {
        if (await UserExists(dto.Username)) return BadRequest("Username is taken");
        
        using var hmac = new HMACSHA512();
        var user = new AppUser
        {
            Username = dto.Username.ToLower(),
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password)),
            PasswordSalt = hmac.Key
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new UserDto()
        {
            Username = user.Username,
            Token = _tokenService.CreateToken(user)
        };
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto dto)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == dto.Username.ToLower());
        if (user is null) return Unauthorized();
        
        using var hmac = new HMACSHA512(user.PasswordSalt);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password));
        for (int i = 0; i < computedHash.Length; i++)
        {
            if (computedHash[i] != user.PasswordHash[i]) return Unauthorized();
        }

        return new UserDto()
        {
            Username = user.Username,
            Token = _tokenService.CreateToken(user)
        };
    }

    private async Task<bool> UserExists(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username.ToLower());
    }
}