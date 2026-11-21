import { Injectable } from '@angular/core';

export interface User {
  id: number;
  name: string;
}

export type Callback = (err: Error | null, data?: { id: number }) => void;

export enum Role {
  Admin,
  User,
}

@Injectable({
  providedIn: 'root',
})
export class UserService {
  private cache: Map<number, User> = new Map();

  constructor(private readonly http: HttpClient, private base: string = '/api') {
    this.cache.clear();
    console.log('init');
  }

  /** Finds a user by id. */
  async findById(id: number): Promise<User | undefined> {
    if (this.cache.has(id)) {
      return this.cache.get(id);
    }
    const res = await this.http.get<User>(`${this.base}/users/${id}`);
    return res;
  }

  public static parse<T extends { id: number }>(raw: string): T {
    const obj = JSON.parse(raw) as T;
    return obj;
  }

  private format(user: User, opts: {
    upper: boolean;
    prefix?: string;
  }): string {
    const name = opts.upper ? user.name.toUpperCase() : user.name;
    return (opts.prefix ?? '') + name;
  }

  get size(): number {
    return this.cache.size;
  }
}

export function load(opts: { url: string; retries: number }): Promise<string> {
  const { url, retries } = opts;
  return fetch(url).then((r) => r.text()).catch(() => `failed ${retries}`);
}

export function configure(
  options: {
    url: string;
    retries: number;
  },
  verbose = false
): void {
  console.log(options.url);
  console.log(verbose);
}

export const makeId = (prefix: string): string => {
  return prefix + Math.random().toString(36).slice(2) + '}';
};

export function Comp(props: { title: string }) {
  const style = { color: 'red' };
  return <div style={style}>{props.title} {"}"} it's</div>;
}

const ratio = (a: number, b: number) => a / b / 2;
