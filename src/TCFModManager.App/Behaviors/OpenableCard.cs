using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace TCFModManager.App.Behaviors;

//
// Fork (1.4.2): a Workshop card - a mod's or a collection's - that opens from the keyboard as well
// as the mouse: Tab to it, then Enter or Space. It is also a button to UI Automation, so a screen
// reader can name it and open it. Either way it raises the MouseLeftButtonUp a click would, so each
// page keeps its one handler for opening a card.
//
public sealed class OpenableCard : Grid
{
    public OpenableCard()
    {
        Focusable = true;
        SetResourceReference(FocusVisualStyleProperty, "SteamModalFocusRing");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None || (e.Key != Key.Enter && e.Key != Key.Space)) return;

        e.Handled = true;
        Open();
    }

    internal void Open() =>
        RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = MouseLeftButtonUpEvent,
            Source = this,
        });

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(OpenableCard card) : FrameworkElementAutomationPeer(card), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(OpenableCard);

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        public void Invoke()
        {
            if (!IsEnabled()) throw new System.Windows.Automation.ElementNotEnabledException();

            card.Dispatcher.BeginInvoke(card.Open);
        }
    }
}
