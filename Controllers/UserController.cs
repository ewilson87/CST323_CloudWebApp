using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CloudWebApp.Models;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CloudWebApp.Controllers
{
    public class UserController : Controller
    {
        private readonly UserManager<UserModel> _userManager;
        private readonly SignInManager<UserModel> _signInManager;
        private readonly ILogger<UserController> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public UserController(UserManager<UserModel> userManager, SignInManager<UserModel> signInManager, ILogger<UserController> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword)
        {
            _logger.LogInformation($"Attempting to register user: {username}, {email}");

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match");
                return View("~/Views/Home/Register.cshtml");
            }

            var user = new UserModel { UserName = username, Email = email };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                _logger.LogInformation($"User {username} created successfully");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
                _logger.LogError($"Error creating user {username}: {error.Description}");
            }

            return View("~/Views/Home/Register.cshtml");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            _logger.LogInformation("Login attempt for user: {Username}", username);

            var user = await _userManager.FindByNameAsync(username);
            if (user != null)
            {
                _logger.LogInformation("User found. EmailConfirmed: {EmailConfirmed}, LockoutEnabled: {LockoutEnabled}, LockoutEnd: {LockoutEnd}",
                    user.EmailConfirmed, user.LockoutEnabled, user.LockoutEnd);

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
                _logger.LogInformation("Is password valid: {IsPasswordValid}", isPasswordValid);

                if (isPasswordValid)
                {
                    var canSignIn = await _signInManager.CanSignInAsync(user);
                    _logger.LogInformation("Can user sign in: {CanSignIn}", canSignIn);

                    if (!canSignIn)
                    {
                        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, password, false);
                        _logger.LogInformation("CheckPasswordSignInAsync result: Succeeded={Succeeded}, IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}, RequiresTwoFactor={RequiresTwoFactor}",
                            signInResult.Succeeded, signInResult.IsLockedOut, signInResult.IsNotAllowed, signInResult.RequiresTwoFactor);
                    }
                }
            }
            else
            {
                _logger.LogWarning("User not found: {Username}", username);
            }

            var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Username} logged in successfully", username);
                return RedirectToAction("Home");
            }

            _logger.LogWarning("Failed login attempt for user: {Username}. Reason: {Reason}", 
                username, 
                result.IsLockedOut ? "Locked out" :
                result.IsNotAllowed ? "Not allowed" :
                result.RequiresTwoFactor ? "Requires two-factor" :
                "Invalid credentials");

            TempData["ErrorMessage"] = "Invalid login attempt.";
            return RedirectToAction("Login", "Home");
        }
    
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User logged out.");
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Home()
        {
            var user = await _userManager.GetUserAsync(User);
            var username = user?.UserName ?? "Unknown";
            _logger.LogInformation("User {Username} accessed Home page", username);

            var model = new UserHomeViewModel
            {
                Username = username
            };

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var model = new UpdateProfileViewModel
            {
                Username = user.UserName,
                Email = user.Email
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
        {
            _logger.LogInformation("UpdateProfile action started for user {UserId}", User.Identity.Name);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid for user {UserId}", User.Identity.Name);
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogWarning("Validation error: {ErrorMessage}", error.ErrorMessage);
                    }
                }
                return View("Profile", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var email = await _userManager.GetEmailAsync(user);
            if (model.Email != email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    foreach (var error in setEmailResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("Profile", model);
                }
            }

            // We're not updating the username, but we're ensuring it's set in the model
            model.Username = user.UserName;

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Your profile has been updated";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Profile", new UpdateProfileViewModel { Username = User.Identity.Name, Email = User.Identity.Name });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View("Profile", new UpdateProfileViewModel { Username = user.UserName, Email = user.Email });
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Your password has been changed.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteAccount(string password)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, password);
            if (!isPasswordCorrect)
            {
                ModelState.AddModelError(string.Empty, "Incorrect password.");
                return View("Profile", new UpdateProfileViewModel { Username = user.UserName, Email = user.Email });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unexpected error occurred deleting user with ID '{_userManager.GetUserId(User)}'.");
            }

            await _signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> GetRandomImage(string keyword)
        {
            var accessKey = _configuration["UnsplashApiKey"];
            var url = $"https://api.unsplash.com/photos/random?query={keyword}&client_id={accessKey}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var imageData = JsonSerializer.Deserialize<JsonElement>(content);
                var imageUrl = imageData.GetProperty("urls").GetProperty("regular").GetString();

                return Json(new { imageUrl });
            }

            return BadRequest("Failed to fetch image");
        }
    }
}
