import React from 'react';

export function TodoCard({ title, tags, isUrgent }) {
  const count = tags.length;
  return (
    <article>
      <h3>{title}</h3>
      {isUrgent && <UrgentIcon />}
      <ul>{tags.map(t => <li key={t}>{t}</li>)}</ul>
      <span>{count}</span>
    </article>
  );
}

export const UrgentIcon = () => {
  const symbol = '!';
  return <i className="urgent">{symbol}</i>;
};
