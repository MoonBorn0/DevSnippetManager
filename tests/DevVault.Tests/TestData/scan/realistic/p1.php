<?php
namespace App\Http;

use App\Models\User;

/**
 * Handles users.
 */
class UserController extends Controller
{
    private array $cache = [];

    public function __construct(private UserRepository $repo)
    {
        $this->cache = [];
        $this->repo = $repo;
    }

    /**
     * Show a user.
     */
    public function show(int $id): string
    {
        $user = $this->repo->find($id);
        $name = "User {$user->name} #{$id}";
        $arr = ['a' => "}", 'b' => '{'];
        return $name . implode(',', $arr);
    }

    # hash comment }
    public static function make(array $opts = []): self
    {
        return new self(new UserRepository($opts));
    }

    protected function helper(): void
    {
        $text = <<<EOT
        Heredoc with { and } braces {$this->cache}
        EOT;
        echo $text;
    }
}

function render_list(array $items): string
{
    $out = '';
    foreach ($items as $i) {
        $out .= "<li>{$i}</li>";
    }
    return $out;
}

interface Shape
{
    public function area(): float;
    public function name(): string;
}

trait Loggable
{
    public function log(string $m): void
    {
        error_log($m);
