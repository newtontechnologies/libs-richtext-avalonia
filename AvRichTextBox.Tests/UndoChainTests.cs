using AvRichTextBox;
using System;
using Xunit;

namespace AvRichTextBox.Tests;

public class UndoStackTests
{
   private sealed class NoopUndo : Undo
   {
      internal NoopUndo(Undo? previous) : base(previous) { }
      public override int UndoEditOffset => 0;
      public override bool UpdateTextRanges => false;
      public override void PerformUndo() { }
   }

   [Fact]
   public void Add_requires_previous_to_be_current_head()
   {
      var stack = new UndoStack();

      var first = new NoopUndo(null);
      stack.Add(first);

      var invalid = new NoopUndo(null);
      Assert.Throws<InvalidOperationException>(() => stack.Add(invalid));

      var second = new NoopUndo(stack.Last());
      stack.Add(second);

      Assert.Equal(2, stack.Count);
      Assert.Same(first, second.Previous);
   }

   [Fact]
   public void RemoveAt_only_allows_removing_most_recent_entry()
   {
      var stack = new UndoStack();
      var first = new NoopUndo(null);
      stack.Add(first);
      stack.Add(new NoopUndo(stack.Last()));

      Assert.Throws<NotSupportedException>(() => stack.RemoveAt(0));

      stack.RemoveAt(stack.Count - 1);
      Assert.Equal(1, stack.Count);
      Assert.Same(first, stack.Last());
   }

   [Fact]
   public void Last_returns_most_recent_entry()
   {
      var stack = new UndoStack();
      var u1 = new NoopUndo(null);
      stack.Add(u1);

      var u2 = new NoopUndo(stack.Last());
      stack.Add(u2);

      Assert.Same(u2, stack.Last());
   }
}


