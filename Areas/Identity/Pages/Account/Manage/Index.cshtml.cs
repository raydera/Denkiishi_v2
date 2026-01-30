using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Denkiishi_v2.Models; // Ajuste para o namespace do teu ApplicationUser
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace Denkiishi_v2.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name = "Nickname / Nome de Exibição")]
            [StringLength(50, ErrorMessage = "O {0} deve ter no máximo {1} caracteres.")]
            public string Nickname { get; set; } // <--- A NOSSA PROPRIEDADE NOVA

            [Phone]
            [Display(Name = "Telefone")]
            public string PhoneNumber { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                Nickname = user.Nickname // <--- CARREGANDO DO BANCO
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            // --- ATUALIZAÇÃO DO NICKNAME ---
            if (Input.Nickname != user.Nickname)
            {
                user.Nickname = Input.Nickname;
                var updateResult = await _userManager.UpdateAsync(user); // Salva no banco
                if (!updateResult.Succeeded)
                {
                    StatusMessage = "Erro inesperado ao atualizar o nickname.";
                    return RedirectToPage();
                }
            }
            // -------------------------------

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "O teu perfil foi atualizado com sucesso!";
            return RedirectToPage();
        }
    }
}