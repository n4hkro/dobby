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
        var viewModel = new LoginViewModel(new RememberMeSavedLogin("user-1", "owner", HasMaskedPasswordPrefill: true));

        viewModel.Username.ShouldBe("owner");
        viewModel.Password.ShouldBe("••••••••");
        viewModel.IsPasswordPrefilled.ShouldBeTrue();
        viewModel.HasStoredPasswordRevealControl.ShouldBeFalse();
        viewModel.LoginWasAttempted.ShouldBeFalse();
    }

    [Fact]
    public void Editing_password_clears_only_prefilled_form_state()
    {
        var viewModel = new LoginViewModel(new RememberMeSavedLogin("user-1", "owner", HasMaskedPasswordPrefill: true));

        viewModel.EditPassword("manual");

        viewModel.Password.ShouldBe("manual");
        viewModel.IsPasswordPrefilled.ShouldBeFalse();
        viewModel.ForgetSavedLoginRequested.ShouldBeFalse();
    }

    [Fact]
    public void Forget_saved_login_clears_persistent_slot_and_form_state_only_when_explicitly_requested()
    {
        var clearer = new RecordingSavedLoginClearer();
        var viewModel = new LoginViewModel(
            new RememberMeSavedLogin("user-1", "owner", HasMaskedPasswordPrefill: true),
            savedLoginClearer: clearer);

        var result = viewModel.ForgetSavedLogin();

        result.IsSuccess.ShouldBeTrue();
        clearer.ForgetCalls.ShouldBe(1);
        viewModel.Username.ShouldBeNull();
        viewModel.Password.ShouldBeNull();
        viewModel.IsPasswordPrefilled.ShouldBeFalse();
        viewModel.ForgetSavedLoginRequested.ShouldBeTrue();
    }

    [Fact]
    public void Display_name_input_is_visible_only_for_first_owner_bootstrap()
    {
        var existingLogin = new LoginViewModel(null);
        var firstOwnerBootstrap = new LoginViewModel(null, showDisplayNameInput: true);

        existingLogin.ShouldShowDisplayNameInput.ShouldBeFalse();
        firstOwnerBootstrap.ShouldShowDisplayNameInput.ShouldBeTrue();
    }

    private sealed class RecordingSavedLoginClearer : ISavedLoginClearer
    {
        public int ForgetCalls { get; private set; }

        public Result ForgetSavedLogin()
        {
            ForgetCalls++;
            return Result.Success();
        }
    }
}
