"""Inventory module."""
import functools
import re


def timed(fn):
    """Decorator that times fn."""
    @functools.wraps(fn)
    def wrapper(*args, **kwargs):
        return fn(*args, **kwargs)
    return wrapper


@timed
def parse(line: str) -> dict:
    """Parse a line.

    Returns a dict with {braces} and "quotes".
    """
    m = re.match(r"(\w+)=(\d+)", line)
    if not m:
        return {}
    return {m.group(1): int(m.group(2))}


# Computes totals
# across all items
def total(items,
          prices=None,
          *, strict=False):
    prices = prices or {}
    s = 0
    for i in items:
        s += prices.get(i, 0)
    return s


class Inventory:
    """Holds items."""

    count = 0

    def __init__(self, name):
        self.name = name
        self.items = {}
        Inventory.count += 1

    @property
    def size(self):
        return len(self.items)

    @size.setter
    def size(self, value):
        raise AttributeError("read only")

    @staticmethod
    def make(name):
        def inner():
            return name
        return Inventory(inner())

    async def fetch(self, url):
        text = f"fetching {url!r} with {{braces}}"
        '''single
        triple'''
        return text

    class Meta:
        ordering = ["name"]
        verbose = True

    def long_string(self):
        s = """
def not_a_def():
    pass
"""
        return s


def tail():
    x = 1
    y = 2
    return x + y


if __name__ == "__main__":
    inv = Inventory("a")
    print(inv.size)
