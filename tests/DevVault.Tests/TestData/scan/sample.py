def compute_total(items):
    """Toplam tutarı hesaplar."""
    def helper(x):
        return x * 2
    return sum(helper(i) for i in items)

class Greeter:
    @staticmethod
    def hello(name):
        """Selamlama metni üretir."""
        msg = f"Merhaba {name}"
        return msg
