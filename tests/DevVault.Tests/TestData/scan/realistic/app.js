'use strict';
const path = require('path');

/**
 * Adds two numbers.
 * @param {number} a
 * @param {number} b
 */
function add(a, b) {
  const s = `sum: ${a + b} {${a}} }`;
  return a + b + s.length;
}

// Greets people
// using a template
const greet = (name) => {
  const re = /[}{]+\/x}/g;
  const str = "}" + '{' + name;
  return `Hello ${name}!` + re.source + str;
};

const double = async x => {
  const y = await Promise.resolve(x);
  return y * 2;
};

function Component({ title, children }) {
  const label = title || 'none';
  return label + children;
}

function withDefaults(opts = {}, flag = false) {
  const merged = Object.assign({}, opts);
  merged.flag = flag;
  return merged;
}

export default function (req, res) {
  res.send('ok');
  res.end();
}

class Animal {
  constructor(name) {
    this.name = name;
    this.sound = 'generic';
  }

  speak() {
    console.log(`${this.name} makes a sound ${this.sound}`);
    return this.name;
  }

  static create(name) {
    const a = new Animal(name);
    return a;
  }

  get label() {
    return `animal:${this.name}`;
  }

  handle = () => {
    console.log('arrow prop');
    return 1;
  };
}

class Dog extends Animal {
  speak() {
    super.speak();
    return 'woof';
  }
}

setTimeout(function () {
  console.log('anonymous callback not extracted');
  console.log('really');
}, 10);

(function () {
  console.log('iife');
  console.log('iife2');
})();

const total = items.reduce((a, b) => a / b / 2, 1);
const t2 = total / 2; const t3 = 10 / 5;

module.exports = {
  add,
  greet,
  helper() {
    return 1;
  },
};
