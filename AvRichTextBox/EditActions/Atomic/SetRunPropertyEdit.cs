using Avalonia;

namespace AvRichTextBox;

internal sealed class SetRunPropertyEdit(EditableRun run, AvaloniaProperty property, object? oldValue, object? newValue) : IAtomicEdit
{
   public void Apply() => run.SetValue(property, newValue);
   public void Unapply() => run.SetValue(property, oldValue);
}


