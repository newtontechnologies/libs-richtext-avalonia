using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace AvRichTextBox;

public partial class FlowDocument
{
   private bool _isTypingEdit;
   private bool _canCoalesceTyping;
   private TypingCoalesceState? _typing;

   internal sealed class TypingCoalesceState
   {
      internal required Paragraph Paragraph;
      internal required EditableRun Run;
      internal required int RunInsertOffsetStart;
      internal required int RunInsertOffsetEnd;
      internal required string OldRunText;
      internal required SelectionState SelectionBefore;
      internal required int RefreshFromBlockIndex;
      internal required List<int> ShiftPoints;
      internal int TotalInsertedChars => RunInsertOffsetEnd - RunInsertOffsetStart;
   }
   internal readonly record struct SelectionState(
      int Start,
      int End,
      ExtendMode ExtendMode,
      bool BiasForwardStart,
      bool BiasForwardEnd
   );

   /// <summary>
   /// Entry point for all user edit operations. Enforces: clear redo stack, put action into redo stack, then apply via Redo().
   /// </summary>
   internal void ExecuteEdit(EditAction action)
   {
      // Any non-coalesced edit breaks the typing coalescing chain.
      _canCoalesceTyping = false;
      _typing = null;
      RedoStack.Clear();
      RedoStack.Push(action);
      Redo();
   }

   internal void Undo()
   {
      if (UndoStack.Count == 0) return;

      var action = UndoStack.Pop();
      action.Unapply(this);
      RedoStack.Push(action);
   }

   internal void Redo()
   {
      if (RedoStack.Count == 0) return;

      var action = RedoStack.Pop();
      action.Apply(this);
      UndoStack.Push(action);
   }

   internal SelectionState CaptureSelectionState()
      => new(Selection.Start, Selection.End, SelectionExtendMode, Selection.BiasForwardStart, Selection.BiasForwardEnd);

   internal void RestoreSelectionState(SelectionState state)
   {
      SelectionExtendMode = state.ExtendMode;
      Selection.BiasForwardStart = state.BiasForwardStart;
      Selection.BiasForwardEnd = state.BiasForwardEnd;
      // Do not rely on Start/End property setters firing (they may be equal and thus not update StartParagraph/EndParagraph
      // after block replacements). Force-refresh selection endpoints.
      SelectionParagraphs.Clear();
      Selection.Start = state.Start;
      Selection.End = state.End;
      SelectionStart_Changed(Selection, state.Start);
      SelectionEnd_Changed(Selection, state.End);
      EnsureSelectionContinuity();
      UpdateSelection();
   }

   internal int ReplaceSelectionWithInlinesCore(int start, int end, List<IEditable> inlinesNormalOrder)
   {
      // Align selection to the intended replace range.
      SelectionExtendMode = ExtendMode.ExtendModeNone;
      SelectionParagraphs.Clear();
      Selection.Start = start;
      Selection.End = Math.Max(start, end);
      SelectionStart_Changed(Selection, Selection.Start);
      SelectionEnd_Changed(Selection, Selection.End);
      EnsureSelectionContinuity();
      UpdateSelection();

      if (inlinesNormalOrder.Count == 0)
      {
         if (Selection.Length > 0)
            DeleteRange(Selection, saveUndo: false);

         SelectionParagraphs.Clear();
         Selection.Start = start;
         Selection.End = start;
         SelectionStart_Changed(Selection, start);
         SelectionEnd_Changed(Selection, start);
         EnsureSelectionContinuity();
         UpdateSelection();
         return 0;
      }

      // SetRangeToInlines expects reverse order (see paste logic).
      var insert = new List<IEditable>(inlinesNormalOrder.Count);
      foreach (var il in inlinesNormalOrder)
         insert.Add(il.Clone());
      insert.Reverse();

      int added = SetRangeToInlines(Selection, insert);
      int newSelPoint = Math.Min(start + added, DocEndPoint - 1);

      SelectionParagraphs.Clear();
      Selection.Start = newSelPoint;
      Selection.End = newSelPoint;
      SelectionStart_Changed(Selection, newSelPoint);
      SelectionEnd_Changed(Selection, newSelPoint);
      EnsureSelectionContinuity();
      UpdateSelection();

      Selection.BiasForwardStart = false;
      Selection.BiasForwardEnd = false;
      SelectionExtendMode = ExtendMode.ExtendModeNone;

      return added;
   }
}


