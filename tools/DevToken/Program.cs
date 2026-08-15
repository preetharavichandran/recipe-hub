using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var sub = args.ElementAtOrDefault(0) ?? "user-a";
var key = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes("RecipeHub-Dev-Signing-Key-At-Least-32-Chars!"));
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
var token = new JwtSecurityToken(
    issuer: "recipehub-dev",
    audience: "recipehub",
    claims: [new Claim("sub", sub)],
    expires: DateTime.UtcNow.AddHours(8),
    signingCredentials: creds);
Console.WriteLine(new JwtSecurityTokenHandler().WriteToken(token));
