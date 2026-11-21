import React, { useState, useEffect } from 'react';

export interface TodoItem {
  id: number;
  text: string;
  done: boolean;
}

export function useTodos(initial: TodoItem[]) {
  const [items, setItems] = useState<TodoItem[]>(initial);
  useEffect(() => {
    setItems(initial);
  }, [initial]);
  return { items, setItems };
}

export function TodoList({ items, showEmpty }: { items: TodoItem[]; showEmpty: boolean }) {
  const active = items.filter(x => !x.done);
  return (
    <section>
      {showEmpty && active.length === 0 && <EmptyBadge />}
      <ul>{items.map(i => <li key={i.id}>{i.text}</li>)}</ul>
    </section>
  );
}

export const EmptyBadge = () => {
  const label = 'Boş';
  return <span className="badge">{label}</span>;
};
