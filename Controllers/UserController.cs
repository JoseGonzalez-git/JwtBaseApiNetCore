using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JwtBaseApiNetCore.Models;
using JwtBaseApiNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Web;
using Amazon.Runtime;

namespace JwtBaseApiNetCore.Controllers
{
    [Route("api")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly MongoDBService _mongoDBService;
        private readonly IConfiguration _config;

        /// <summary>
        /// Verifica algun campo de una  lista de string viene vacío o es null
        /// </summary>
        /// <param name="fields"></param>
        /// <returns>True si un campo está vacío o es null</returns>
        private static bool ValidateFields(List<string> fields)
        {
            foreach (string field in fields)
            {
                if (String.IsNullOrEmpty(field.Trim()))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null || a.Length != b.Length)
                return false;

            bool areEqual = true;
            for (int i = 0; i < a.Length; i++)
            {
                areEqual &= (a[i] == b[i]);
            }

            return areEqual;
        }
        private string GenerateToken(string email)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

                SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JWT:Key"]));
                SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                JwtSecurityToken token = new JwtSecurityToken(_config["JWT:Issuer"],
                  _config["JWT:Audience"],
                  claims,
                  expires: DateTime.Now.AddMinutes(30),
                  signingCredentials: creds);

                string strToken = new JwtSecurityTokenHandler().WriteToken(token);

                return strToken;
            }
            catch (Exception e)
            {
                return String.Empty;
            }
        }
        public UsersController(IConfiguration config, MongoDBService mongoDBService)
        {
            _mongoDBService = mongoDBService;
            _config = config;
        }

        [Authorize]
        [HttpGet("users")]
        public async Task<List<User>> Get()
        {
            try
            {
                return await _mongoDBService.GetAsyncUsers();
            }
            catch (Exception e)
            {
                return new List<User>();
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Registro([FromBody] RegisterRequest user)
        {
            try
            {
                if (ValidateFields(new List<string> { user.Username, user.Email, user.Phone, user.Password }))
                    return BadRequest("Es necesario que ingrese todos los campos que sean obligatorios");

                User userDB = new User() { Username = user.Username, Email = user.Email, Phone = user.Phone, Password = user.Password };
                // Generación del hash y la sal
                using (var hashAlgorithm = new Rfc2898DeriveBytes(user.Password, 32, 10000))
                {
                    byte[] salt = hashAlgorithm.Salt;
                    byte[] hash = hashAlgorithm.GetBytes(256 / 8);

                    // Almacenamiento del hash y la sal en la base de datos
                    userDB.Password = Convert.ToBase64String(hash);
                    userDB.KeyWordHash = Convert.ToBase64String(salt);

                }

                await _mongoDBService.AddUserAsync(userDB);
                return CreatedAtAction(nameof(Get), new { id = userDB.Id }, userDB);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            try
            {
                User UserDB = await _mongoDBService.GetAsyncOneUser(loginRequest.Email);

                if (UserDB == null)
                {
                    return Unauthorized();
                }

                // Verificación de la contraseña
                using (var hashAlgorithm = new Rfc2898DeriveBytes(loginRequest.Password, Convert.FromBase64String(UserDB.KeyWordHash), 10000))
                {
                    byte[] providedHash = hashAlgorithm.GetBytes(256 / 8);
                    bool isPasswordCorrect = ByteArraysEqual(Convert.FromBase64String(UserDB.Password), providedHash);

                    if (!isPasswordCorrect)
                    {
                        return Unauthorized("Credenciales incorrectas");
                    }
                }

                var response = new
                {
                    token = GenerateToken(UserDB.Email)
                };
                return Ok(response);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }


        [Authorize]
        [HttpPost("reload_token")]
        public IActionResult RenewToken([FromBody] SessionRequest sessionRequest)
        {
            try
            {
                if (ValidateFields(new List<string> { sessionRequest.Email })) return BadRequest("Es necesario agregar una direccion de correo valida");

                string newToken = GenerateToken(sessionRequest.Email);

                if (string.IsNullOrEmpty(newToken)) throw new Exception("Ocurrió un error al momento de generar el token");
                var newTokenExpirationTime = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

                return Ok(new
                {
                    Token = newToken,
                    Expiration = newTokenExpirationTime
                });
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }

        }
        /*
        [Authorize]
        [HttpGet("{id:length(24)}", Name = "GetUser")]
        public ActionResult<User> Get(string id)
        {
            var User = _user.Find(u => u.Id == id).FirstOrDefault();
            if (User == null)
            {
                return NotFound();
            }

            return User;
        }

        [Authorize]
        [HttpPut("{id:length(24)}")]
        public IActionResult Update(string id, User UserIn)
        {
            var User = _user.Find(u => u.Id == id).FirstOrDefault();

            if (User == null)
            {
                return NotFound();
            }

            _user.ReplaceOne(u => u.Id == User.Id, UserIn);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id:length(24)}")]
        public IActionResult Delete(string id)
        {
            var User = _user.Find(u => u.Id == id).FirstOrDefault();

            if (User == null)
            {
                return NotFound();
            }

            _user.DeleteOne(u => u.Id == User.Id);

            return NoContent();
        }
        */
    }
}
