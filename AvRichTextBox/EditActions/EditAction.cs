using System.Collections.Generic;

namespace AvRichTextBox;

/// <summary>
/// Represents one reversible edit operation as an ordered list of simple atomic edits.
/// Apply runs edits in order, Unapply in reverse order.
/// </summary>
internal sealed class EditAction
{
   private readonly IReadOnlyList<IAtomicEdit> _edits;
   private readonly FlowDocument.SelectionState _selectionBefore;
   private readonly FlowDocument.SelectionState _selectionAfter;
   private readonly int _refreshFromBlockIndex;
   private readonly IReadOnlyList<Paragraph> _refreshParagraphs;

   internal EditAction(
      IReadOnlyList<IAtomicEdit> edits,
      FlowDocument.SelectionState selectionBefore,
      FlowDocument.SelectionState selectionAfter,
      int refreshFromBlockIndex,
      IReadOnlyList<Paragraph> refreshParagraphs)
   {
      _edits = edits;
      _selectionBefore = selectionBefore;
      _selectionAfter = selectionAfter;
      _refreshFromBlockIndex = refreshFromBlockIndex;
      _refreshParagraphs = refreshParagraphs;
   }

   internal void Apply(FlowDocument doc)
   {
      for (int i = 0; i < _edits.Count; i++)
         _edits[i].Apply();

      doc.RestoreSelectionState(_selectionAfter);
      doc.RefreshAfterAtomicEdits(_refreshFromBlockIndex, _refreshParagraphs);
   }

   internal void Unapply(FlowDocument doc)
   {
      for (int i = _edits.Count - 1; i >= 0; i--)
         _edits[i].Unapply();

      doc.RestoreSelectionState(_selectionBefore);
      doc.RefreshAfterAtomicEdits(_refreshFromBlockIndex, _refreshParagraphs);
   }
}
