export interface User {
    id: number;
    name: string;
    email?: string;
}

export class UserService {
    /** Kullanıcıyı bulur */
    async findById(id: number): Promise<User | null> {
        const res = await fetch(`/api/users/${id}`);
        return res.ok ? await res.json() : null;
    }
}
