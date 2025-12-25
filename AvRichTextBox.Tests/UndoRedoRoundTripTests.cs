using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.Threading.Tasks;
using Xunit;

namespace AvRichTextBox.Tests;

public class UndoRedoRoundTripTests
{
   private static async Task StabilizeAsync()
   {
      // FlowDocument has some async initialization for caret/layout; give it a moment in tests.
      await Task.Delay(120);
   }

   [Fact]
   public async Task InsertText_roundtrips_via_undo_redo()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("abc");
      var s1 = doc.SerializeForTests();

      Assert.True(doc.CanUndo);
      doc.Undo();
      Assert.True(doc.CanRedo);
      doc.Redo();

      var s2 = doc.SerializeForTests();
      Assert.Equal(s1, s2);
   }

   [Fact]
   public async Task Typing_two_chars_coalesces_into_one_action_and_one_run()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("a");
      doc.InsertText("b");

      Assert.Equal(1, doc.UndoCount);
      var s = doc.SerializeForTests();

      Assert.Contains("T=\"ab\"", s);
      Assert.DoesNotContain("T=\"a\"", s);
      Assert.DoesNotContain("T=\"b\"", s);
   }

   [Fact]
   public async Task Redo_stack_is_cleared_by_new_edit()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("a");
      // Move caret away and back to break typing coalescing on purpose.
      doc.MoveSelectionLeft(false);
      doc.MoveSelectionRight(false);
      doc.InsertText("b");
      Assert.Equal(2, doc.UndoCount);
      Assert.Equal(0, doc.RedoCount);

      doc.Undo();
      Assert.Equal(1, doc.UndoCount);
      Assert.Equal(1, doc.RedoCount);

      doc.InsertText("c"); // new edit
      Assert.Equal(2, doc.UndoCount);
      Assert.Equal(0, doc.RedoCount);
   }

   [Fact]
   public async Task Multi_undo_redo_restores_exact_state()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("a");
      doc.MoveSelectionLeft(false);
      doc.MoveSelectionRight(false);
      doc.InsertText("b");
      doc.MoveSelectionLeft(false);
      doc.MoveSelectionRight(false);
      doc.InsertText("c");
      var sAfter = doc.SerializeForTests();

      doc.Undo();
      doc.Undo();
      Assert.True(doc.CanRedo);
      Assert.Equal(1, doc.UndoCount);
      Assert.Equal(2, doc.RedoCount);

      doc.Redo();
      doc.Redo();
      Assert.False(doc.CanRedo);
      Assert.Equal(3, doc.UndoCount);
      Assert.Equal(0, doc.RedoCount);

      Assert.Equal(sAfter, doc.SerializeForTests());
   }

   [Fact]
   public async Task InsertParagraph_then_delete_selection_roundtrips()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("ab");

      // Insert paragraph between 'a' and 'b'
      doc.ExecuteEdit(doc.BuildReplaceRangeAction(1, 1, [new EditableRun("\r")]));
      var sBeforeDelete = doc.SerializeForTests();

      // Delete across the paragraph boundary
      doc.Select(0, 2);
      doc.DeleteSelection();
      var sAfterDelete = doc.SerializeForTests();

      doc.Undo();
      doc.Redo();

      Assert.NotEqual(sBeforeDelete, sAfterDelete);
      Assert.Equal(sAfterDelete, doc.SerializeForTests());
   }

   [Fact]
   public async Task ApplyFormatting_roundtrips()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("hello");

      doc.Select(0, 5);
      doc.Selection.ApplyFormatting(Inline.FontWeightProperty, FontWeight.Bold);
      var s1 = doc.SerializeForTests();

      doc.Undo();
      doc.Redo();
      Assert.Equal(s1, doc.SerializeForTests());
   }

   [Fact]
   public async Task Backspace_at_start_of_second_paragraph_deletes_paragraph_break_and_merges()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      // Create two paragraphs: "a" + paragraph break + "b"
      doc.Select(0, 0);
      doc.ExecuteEdit(doc.BuildReplaceRangeAction(0, 0, [new EditableRun("a\r"), new EditableRun("b")]));

      // Start of second paragraph is index 2: [ 'a'(0) ][ '\\r'(1) ][ 'b'(2) ]
      doc.Select(2, 0);
      doc.DeleteChar(backspace: true);

      var s = doc.SerializeForTests();
      Assert.Contains("Blocks=1", s);
      Assert.StartsWith("ab\r", doc.Text);
   }

   [Fact]
   public async Task Delete_at_end_of_first_paragraph_deletes_paragraph_break_and_merges()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.ExecuteEdit(doc.BuildReplaceRangeAction(0, 0, [new EditableRun("a\r"), new EditableRun("b")]));

      // End of first paragraph text is index 1 (paragraph break position).
      doc.Select(1, 0);
      doc.DeleteChar(backspace: false);

      var s = doc.SerializeForTests();
      Assert.Contains("Blocks=1", s);
      Assert.StartsWith("ab\r", doc.Text);
   }

   [Fact]
   public async Task Enter_in_middle_of_run_moves_suffix_to_new_paragraph()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("ab");

      // caret between a and b
      doc.Select(1, 0);
      doc.ExecuteEdit(doc.BuildReplaceRangeAction(1, 1, [new EditableRun("\r")]));

      Assert.StartsWith("a\rb\r", doc.Text);
   }

   [Fact]
   public async Task Enter_with_selection_moves_selected_and_suffix_to_new_paragraph()
   {
      var doc = new AvRichTextBox.FlowDocument();
      await StabilizeAsync();

      doc.Select(0, 0);
      doc.InsertText("abc");

      // select "b"
      doc.Select(1, 1);
      doc.ExecuteEdit(doc.BuildReplaceRangeAction(1, 2, [new EditableRun("\r")]));

      Assert.StartsWith("a\rbc\r", doc.Text);
   }
}


