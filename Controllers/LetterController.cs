using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Serv.Controllers
{
	[ApiController]
	[Route("API")]
	public class LetterController : ControllerBase
	{
		const string path_to_db = "/opt/sqlite/tiu_2";
		const string path_to_img = "/opt/sqlite/img";
		const string table_name = "letters";
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

		public Int32 use_db(String query)
		{
			System.Console.WriteLine(query);
			using (var connection = new SqliteConnection($"Data Source={path_to_db}"))
			{
				connection.Open();

				var command = connection.CreateCommand();
				command.CommandText = query;
				return command.ExecuteNonQuery();
			}
		}

		public Letter get_letter(Int32 id)
		{
			Letter result = new Letter();
			result.id = id.ToString();
			use_db($"SELECT c, desc, path FROM {table_name} WHERE id={id.ToString()}", new every_row((SqliteDataReader row) =>
			{
				result.c = row.GetChar(0);
				result.description = row.GetString(1);
				if(!row.IsDBNull(2)) result.image_path = row.GetString(2);
			}));
			return result;
		}

		[HttpGet("letter/{id}"), Authorize]
		public IActionResult single(Int32 id)
		{
			Letter result = get_letter(id);
			if (result.description == null) return NotFound($"id = {id}");
			else return new JsonResult(result);
		}

		[HttpGet("letters"), Authorize]
		public JsonResult all(int? limit = 100)
		{
			List<Letter> letters = new List<Letter>();
			use_db($"SELECT id, c, path FROM {table_name} LIMIT {limit.ToString()}", new every_row((SqliteDataReader row) =>
			{
				Letter tmp = new Letter();
				tmp.id = row.GetInt32(0).ToString();
				tmp.c = row.GetChar(1);
				tmp.description = null; // if it's only for listing, why send description?
				if(!row.IsDBNull(2)) tmp.image_path = row.GetString(2);
				letters.Add(tmp);
			}));
			return new JsonResult(letters);
		}

		[HttpPost("edit_letter"), Authorize("admin")]
		public IActionResult edit(Letter edited_one)
		{
			use_db($"UPDATE {table_name} SET c='{edited_one.c}', desc='{edited_one.description}' WHERE id={edited_one.id}");
			return Ok();
		}

		[HttpPost("add_letter"), Authorize("admin")]
		public String add(Letter new_one)
		{
			use_db($"INSERT INTO {table_name}(c, desc) VALUES('{new_one.c}', '{new_one.description}')");
			Int32 val = 0;
			use_db($"SELECT id FROM {table_name} WHERE c='{new_one.c}'", new every_row((SqliteDataReader row) => {val = row.GetInt32(0);}));
			return val.ToString();
		}

		[HttpDelete("remove_letter/{id}"), Authorize("admin")]
		public IActionResult remove(Int32 id)
		{
			if(use_db($"DELETE FROM {table_name} WHERE id={id.ToString()}") != 1) return StatusCode(304, "Not Modified");
			return Ok();
		}

		[HttpPost("upload/{id}"), DisableRequestSizeLimit, Authorize("admin")]
		public IActionResult upload_pic(String id)
		{
			try
			{
				IFormFile file = Request.Form.Files[0];
				string path_to_save = Path.Combine(path_to_img, id);
				if (file.Length > 0)
				{
					string file_name = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"').Replace(' ', '_');
					string final_file_path = $"{path_to_save}{Path.GetExtension(file_name)}";
					use_db($"SELECT id, c FROM letters WHERE c='{id}'", new every_row((SqliteDataReader row) => {
						Console.WriteLine($"{row.GetInt32(0)} | {row.GetString(1)}");
					}));
					Console.WriteLine($"VALUE: {use_db($"UPDATE letters SET path='{final_file_path}' WHERE c='{id}'")}");
					using (var stream = new FileStream(final_file_path, FileMode.Create))
					{
						file.CopyTo(stream);
					}
					return Ok();
				}
				else
				{
					return BadRequest();
				}
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error: {ex}");
			}
		}

		[HttpGet("pic/{id}/{random_stuff}"), Authorize]
		public IActionResult get_pic(Int32 id, String random_stuff)
		{
			string path = get_letter(id).image_path;
			if(!System.IO.File.Exists(path)) return StatusCode(404, "File not found");	
			byte[] data;
			try
			{
				data = System.IO.File.ReadAllBytes(path);
			}
			catch(Exception)
			{
				return StatusCode(204, "File not found");
			}

			var tmp =  new FileContentResult(data, "application/octet-stream");
			string[] splitted_path = path.Split(Path.DirectorySeparatorChar);
			tmp.FileDownloadName = splitted_path[splitted_path.Length - 1];
			return tmp;
		}
	}
}
