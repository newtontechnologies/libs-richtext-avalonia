using System;

namespace AvRichTextBox;

internal sealed class SetRunTextEdit(EditableRun run, string oldText, string newText) : IAtomicEdit
{
   public void Apply() => run.InlineText = newText;
   public void Unapply() => run.InlineText = oldText;
}


