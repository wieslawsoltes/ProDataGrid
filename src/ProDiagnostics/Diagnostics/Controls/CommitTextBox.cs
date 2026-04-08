using System;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace Avalonia.Diagnostics.Controls
{
    //TODO: UpdateSourceTrigger & Binding.ValidationRules could help removing the need for this control.
    partial class CommitTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        public Func<string?, Exception?>? CommitValidator { get; set; }

        /// <summary>
        ///     Defines the <see cref="CommittedText" /> property.
        /// </summary>
        public static readonly DirectProperty<CommitTextBox, string?> CommittedTextProperty =
            AvaloniaProperty.RegisterDirect<CommitTextBox, string?>(
                nameof(CommittedText), o => o.CommittedText, (o, v) => o.CommittedText = v);

        private static readonly object StaleContext = new();
        private string? _committedText;
        private object? _editContext;

        public string? CommittedText
        {
            get => _committedText;
            set => SetAndRaise(CommittedTextProperty, ref _committedText, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == CommittedTextProperty)
            {
                Text = CommittedText;
                _editContext = null;
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_editContext != null && !ReferenceEquals(_editContext, DataContext))
            {
                _editContext = StaleContext;
            }
        }

        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);

            _editContext = DataContext;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            switch (e.Key)
            {
                case Key.Enter:

                    TryCommit();

                    e.Handled = true;

                    break;

                case Key.Escape:

                    Cancel();

                    e.Handled = true;

                    break;
            }
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);

            TryCommit();
            _editContext = null;
        }

        private void Cancel()
        {
            Text = CommittedText;
            DataValidationErrors.ClearErrors(this);
        }

        private void TryCommit()
        {
            if (_editContext == StaleContext || (_editContext != null && !ReferenceEquals(_editContext, DataContext)))
            {
                Cancel();
                _editContext = null;
                return;
            }

            var validationError = CommitValidator?.Invoke(Text);
            if (validationError != null)
            {
                DataValidationErrors.SetError(this, validationError);
                return;
            }

            if (DataValidationErrors.GetHasErrors(this))
            {
                Text = CommittedText;
                DataValidationErrors.ClearErrors(this);
                return;
            }

            CommittedText = Text;
            BindingOperations.GetBindingExpressionBase(this, CommittedTextProperty)?.UpdateSource();
        }
    }
}
