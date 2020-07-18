using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin.Security.Jwt;

namespace Serv.Controllers
{
	[ApiController]
	[Route("login")]
	public class LoginController : ControllerBase
	{
		const string path_to_db = "/opt/sqlite/tiu_2";
		public static string admin_role = "admin";
		public static string user_role = "user";

		[HttpPost("login")]
		// [EnableCors("developerska")]
		public IActionResult Login(string user, string pass)
		{
			if (user == null)
			{
				return BadRequest("Invalid client request");
			}
			else
			{
				string role = exists(user, pass);
				if (role != null)
				{
					var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("superSecretKey@345"));
					var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
					var claims = new List<Claim>
					{
						new Claim(ClaimTypes.Name, user),
						new Claim(ClaimTypes.Role, role)
					};
					var tokeOptions = new JwtSecurityToken(
						issuer: "http://localhost:4300",
						audience: "http://localhost:4300",
						claims: claims,
						expires: DateTime.Now.AddYears(1),
						signingCredentials: signinCredentials
					);
					var tokenString = new JwtSecurityTokenHandler().WriteToken(tokeOptions);
					return Ok(new { tkn = tokenString, role = role });
				}
				else
				{
					return Unauthorized();
				}
			}
		}

		// helpers
		public delegate void every_row(SqliteDataReader row);

		public void use_db(String query, every_row row)
		{
			System.Console.WriteLine(query);
			using (var connection = new SqliteConnection($"Data Source={path_to_db}"))
			{
				connection.Open();

					var command = connection.CreateCommand();
				command.CommandText = query;
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read()) if(!reader.IsDBNull(0)) row(reader);
				}
			}
		}

		string exists(string user, string pass)
		{
			string ret = null;
			// most sql injectible code ever
			use_db($"SELECT role FROM users WHERE user='{user}' AND pass='{pass}'", new every_row((SqliteDataReader row)=>{
				if(!row.IsDBNull(0)) ret = row.GetString(0);
			}));
			return ret;
		}
	}
}