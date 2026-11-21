// İki sayıyı çarpar
function multiply(a, b) {
    const result = a * b;
    return result;
}

const formatName = (first, last) => {
    const full = `${first} ${last}`;
    return full.trim();
};

class Cart {
    addItem(item) {
        this.items.push(item);
        return this.items.length;
    }
}

export default function () {
    const value = 42;
    return value;
}
