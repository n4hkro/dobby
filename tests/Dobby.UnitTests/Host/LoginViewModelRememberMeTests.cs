using Dobby.Host.Login;
using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LoginViewModelRememberMeTests
{
    [Fact]
    public void Saved_slot_prefills_masked_password_without_auto_login_or_reveal_control()
    {
        var viewModel = new LoginViewModel(new RememberMeSavedLogin(
            "user-1",
            "owner",
            "correct horse battery staple",
            HasMaskedPasswordPrefill: true));

        viewModel.Username.ShouldBe("owner");
        viewModel.Password.ShouldBe("correct horse battery staple");
        viewModel.RememberMeSelected.ShouldBeTrue();
        viewModel.IsPasswordPrefilled.ShouldBeTrue();
        viewModel.HasStoredPasswordRevealControl.ShouldBeFalse();
        viewModel.LoginWasAttempted.ShouldBeFalse();
    }

    [Fact]
    public void Saved_password_prefill_uses_fixed_display_mask_without_revealing_saved_length()
    {
        var shortCredential = new LoginViewModel(new RememberMeSavedLogin(
            "user-1",
            "owner",
            "x",
            HasMaskedPasswordPrefill: true));
        var longCredential = new LoginViewModel(new RememberMeSavedLogin(
            "user-1",
            "owner",
            "correct horse battery staple",
            HasMaskedPasswordPrefill: true));

        shortCredential.InitialPasswordDisplayValue.ShouldBe(LoginViewModel.SavedPasswordPlaceholder);
        longCredential.InitialPasswordDisplayValue.ShouldBe(LoginViewModel.SavedPasswordPlaceholder);
        shortCredential.InitialPasswordDisplayValue.ShouldNotBe(shortCredential.Password);
        longCredential.InitialPasswordDisplayValue!.Length.ShouldNotBe(longCredential.Password!.Length);
    }

    [Fact]
    public void Saved_password_prefill_submits_actual_saved_password_until_user_edits_field()
    {
        var viewModel = new LoginViewModel(new RememberMeSavedLogin(
            "user-1",
            "owner",
            "saved password",
            HasMaskedPasswordPrefill: true));

        viewModel.SubmittedPassword(LoginViewModel.SavedPasswordPlaceholder).ShouldBe("saved password");

        viewModel.EditPassword("manual");

        viewModel.SubmittedPassword("manual").ShouldBe("manual");
    }

    [Fact]
    public void Startup_prefill_displays_saved_login_without_resolving_secret_until_submit()
    {
        var resolveCalls = 0;
        var viewModel = LoginViewModel.CreateStartupPrefill("owner", () =>
        {
            resolveCalls++;
            return "saved password";
        });

        viewModel.Username.ShouldBe("owner");
        viewModel.RememberMeSelected.ShouldBeTrue();
        viewModel.IsPasswordPrefilled.ShouldBeTrue();
        viewModel.InitialPasswordDisplayValue.ShouldBe(LoginViewModel.SavedPasswordPlaceholder);
        resolveCalls.ShouldBe(0);

        viewModel.SubmittedPassword(LoginViewModel.SavedPasswordPlaceholder).ShouldBe("saved password");
        resolveCalls.ShouldBe(1);
    }

    [Fact]
    public void Editing_password_clears_only_prefilled_form_state()
    {
        var viewModel = new LoginViewModel(new RememberMeSavedLogin(
            "user-1",
            "owner",
            "saved password",
            HasMaskedPasswordPrefill: true));

        viewModel.EditPassword("manual");

        viewModel.Password.ShouldBe("manual");
        viewModel.IsPasswordPrefilled.ShouldBeFalse();
    }

    [Fact]
    public void Login_view_model_does_not_expose_explicit_saved_login_forget_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "Login", "LoginViewModel.cs"));

        source.ShouldNotContain(string.Concat("ISaved", "Login", "Clearer"));
        source.ShouldNotContain(string.Concat("Forget", "Saved", "Login"));
        source.ShouldNotContain(string.Concat("saved", "Login", "Clearer"));
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
