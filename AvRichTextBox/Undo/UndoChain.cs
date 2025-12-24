using System;
using System.Collections.Generic;

namespace AvRichTextBox;

/// <summary>
/// A linear undo history stored as a backward linked list.
/// Each record has an immutable reference to its predecessor.
/// The predecessor relationship is unique (a record cannot be attached to an arbitrary node).
///
/// Nodes also track their successor(s) so alternative futures can be kept alive by holding references,
/// even though the document itself advances only by appending to the current head.
/// </summary>
internal sealed class UndoChain
{
   internal sealed class Node
   {
      internal Node(IUndo value, Node? previous)
      {
         Value = value ?? throw new ArgumentNullException(nameof(value));
         Previous = previous;
         previous?.Next.Add(this);
      }

      internal IUndo Value { get; }
      internal Node? Previous { get; }
      internal List<Node> Next { get; } = [];
   }

   private Node? _head;

   internal int Count { get; private set; }
   internal Node? HeadNode => _head;

   internal void Clear()
   {
      _head = null;
      Count = 0;
   }

   internal void Add(IUndo undo)
   {
      _head = new Node(undo, _head);
      Count++;
   }

   internal IUndo Last()
   {
      if (_head is null) throw new InvalidOperationException("The undo chain is empty.");
      return _head.Value;
   }

   /// <summary>
   /// Only removing the most recent undo record is supported.
   /// </summary>
   internal void RemoveAt(int index)
   {
      if (index != Count - 1)
         throw new NotSupportedException("UndoChain only supports removing the most recent entry.");

      if (_head is null) throw new InvalidOperationException("The undo chain is empty.");

      _head = _head.Previous;
      Count--;
   }
}


