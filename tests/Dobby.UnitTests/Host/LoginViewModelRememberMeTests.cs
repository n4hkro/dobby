using Dobby.Host.Login;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LoginViewModelRememberMeTests
{
    [Fact]
    public void Remembered_account_prefills_only_username_and_never_password()
    {
        var viewModel = new LoginViewModel(new RememberedLoginAccount("user-1", "owner"));

        viewModel.Username.ShouldBe("owner");
        viewModel.Password.ShouldBeNull();
        viewModel.RememberMeSelected.ShouldBeTrue();
        viewModel.LoginWasAttempted.ShouldBeFalse();
    }

    [Fact]
    public void Remembered_account_requires_new_password_input()
    {
        var viewModel = new LoginViewModel(new RememberedLoginAccount("user-1", "owner"));

        viewModel.EditPassword("manual");

        viewModel.Password.ShouldBe("manual");
    }

    [Fact]
    public void Login_view_model_source_has_no_password_resolver_or_saved_password_placeholder()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "Login", "LoginViewModel.cs"));

        source.ShouldNotContain("SavedPasswordPlaceholder");
        source.ShouldNotContain("passwordResolver");
        source.ShouldNotContain("SubmittedPassword");
        source.ShouldNotContain("RememberMeSavedLogin");
    }

    [Fact]
    public void Display_name_input_is_visible_only_for_first_owner_bootstrap()
    {
        var existingLogin = new LoginViewModel(null);
        var firstOwnerBootstrap = new LoginViewModel(null, showDisplayNameInput: true);

        existingLogin.ShouldShowDisplayNameInput.ShouldBeFalse();
        firstOwnerBootstrap.ShouldShowDisplayNameInput.ShouldBeTrue();
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
