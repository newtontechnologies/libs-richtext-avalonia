using AvRichTextBox;
using System;
using Xunit;

namespace AvRichTextBox.Tests;

public class UndoChainTests
{
   private sealed class NoopUndo : IUndo
   {
      public int UndoEditOffset => 0;
      public bool UpdateTextRanges => false;
      public void PerformUndo() { }
   }

   [Fact]
   public void Add_builds_linear_chain_with_unique_predecessor()
   {
      var chain = new UndoChain();

      chain.Add(new NoopUndo());
      var first = chain.HeadNode!;

      chain.Add(new NoopUndo());
      var second = chain.HeadNode!;

      Assert.Equal(2, chain.Count);
      Assert.Same(first, second.Previous);
      Assert.Single(first.Next);
      Assert.Same(second, first.Next[0]);
   }

   [Fact]
   public void RemoveAt_only_allows_removing_most_recent_entry()
   {
      var chain = new UndoChain();
      chain.Add(new NoopUndo());
      chain.Add(new NoopUndo());

      Assert.Throws<NotSupportedException>(() => chain.RemoveAt(0));

      chain.RemoveAt(chain.Count - 1);
      Assert.Equal(1, chain.Count);
   }

   [Fact]
   public void Last_returns_most_recent_entry()
   {
      var chain = new UndoChain();
      var u1 = new NoopUndo();
      var u2 = new NoopUndo();

      chain.Add(u1);
      chain.Add(u2);

      Assert.Same(u2, chain.Last());
   }
}


