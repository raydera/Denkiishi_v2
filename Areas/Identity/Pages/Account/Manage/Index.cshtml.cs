using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Denkiishi_v2.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Página &quot;Perfil&quot; do Identity: edição de nickname, telefone e preferência de frequência dos lembretes SRS por e-mail.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        /// <summary>
        /// Construtor padrão do modelo de página Razor com os managers do ASP.NET Core Identity.
        /// </summary>
        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        /// <summary>
        /// Nome de usuário (login) exibido como somente leitura na UI.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Mensagem temporária após operações de gravação (sucesso ou erro).
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        /// Modelo ligado ao formulário POST.
        /// O binder do ASP.NET Core preenche <see cref="InputModel"/> a partir dos campos name gerados por <c>asp-for</c> na view
        /// (ex.: <c>Input.Nickname</c>, <c>Input.NotificationFrequencyId</c>). Validações em <see cref="InputModel"/> rodam antes de <see cref="OnPostAsync"/>.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// Campos editáveis do perfil: valores enviados pelo cliente no POST são validados e aplicados sobre <see cref="ApplicationUser"/>.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            /// Apelido opcional para exibição na aplicação.
            /// </summary>
            [Display(Name = "Nickname / Nome de Exibição")]
            [StringLength(50, ErrorMessage = "O {0} deve ter no máximo {1} caracteres.")]
            public string Nickname { get; set; }

            /// <summary>
            /// Telefone opcional gerenciado pelo Identity (<c>PhoneNumber</c> na tabela de usuários).
            /// </summary>
            [Phone]
            [Display(Name = "Telefone")]
            public string PhoneNumber { get; set; }

            /// <summary>
            /// Preferência de cadência dos lembretes SRS (espelha <c>AspNetUsers.notification_frequency_id</c>).
            /// O &lt;select&gt; na view envia o valor inteiro (0–3); o binder converte para este <see cref="int"/>.
            /// </summary>
            [Display(Name = "Lembretes de revisão SRS por e-mail")]
            [Range(0, 3, ErrorMessage = "Selecione uma frequência válida.")]
            public int NotificationFrequencyId { get; set; } = 1;
        }

        /// <summary>
        /// Carrega <see cref="Username"/> e <see cref="Input"/> a partir da entidade persistida para exibir valores atuais no GET e repovoar o formulário após erro de validação.
        /// </summary>
        /// <param name="user">Usuário autenticado retornado por <see cref="UserManager{TUser}.GetUserAsync"/>.</param>
        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                Nickname = user.Nickname,
                NotificationFrequencyId = user.NotificationFrequencyId
            };
        }

        /// <summary>
        /// GET: exibe o formulário com dados atuais do usuário, incluindo <see cref="ApplicationUser.NotificationFrequencyId"/>.
        /// </summary>
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

        /// <summary>
        /// POST: persiste alterações de nickname, telefone e frequência de notificação SRS quando o modelo é válido.
        /// <see cref="Input.NotificationFrequencyId"/> veio do campo &lt;select&gt; via model binding; atualizamos <see cref="ApplicationUser.NotificationFrequencyId"/> apenas se mudou, para evitar writes desnecessários.
        /// </summary>
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

            var profileDirty = false;

            if (Input.Nickname != user.Nickname)
            {
                user.Nickname = Input.Nickname;
                profileDirty = true;
            }

            if (Input.NotificationFrequencyId != user.NotificationFrequencyId)
            {
                user.NotificationFrequencyId = Input.NotificationFrequencyId;
                profileDirty = true;
            }

            if (profileDirty)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    StatusMessage = "Erro inesperado ao atualizar o perfil.";
                    return RedirectToPage();
                }
            }

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
