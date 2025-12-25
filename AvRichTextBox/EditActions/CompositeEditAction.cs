using System;
using System.Collections.Generic;

namespace AvRichTextBox;

internal sealed class CompositeEditAction : EditAction
{
   private readonly IReadOnlyList<IAtomicEdit> _edits;
   private readonly FlowDocument.SelectionState _selectionBefore;
   private readonly FlowDocument.SelectionState _selectionAfter;
   private readonly int _refreshFromBlockIndex;
   private readonly IReadOnlyList<Paragraph> _refreshParagraphs;

   internal CompositeEditAction(
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

   internal override void Apply(FlowDocument doc)
   {
      for (int i = 0; i < _edits.Count; i++)
         _edits[i].Apply();

      doc.RestoreSelectionState(_selectionAfter);
      doc.RefreshAfterAtomicEdits(_refreshFromBlockIndex, _refreshParagraphs);
   }

   internal override void Unapply(FlowDocument doc)
   {
      for (int i = _edits.Count - 1; i >= 0; i--)
         _edits[i].Unapply();

      doc.RestoreSelectionState(_selectionBefore);
      doc.RefreshAfterAtomicEdits(_refreshFromBlockIndex, _refreshParagraphs);
   }
}


