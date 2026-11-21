export function Comp({
  title,
  children,
  extra,
}) {
  const label = title || "none";
  return label + children;
}

export default function Page({ a, b }) {
  const x = a;
  const y = b;
  return x + y;
}

const Button = ({
  label,
  onClick,
}) => {
  const t = label;
  return t + onClick;
};
