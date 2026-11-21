#include <stdio.h>
#include <stdlib.h>
#define MAX(a, b) ((a) > (b) ? (a) : (b))
#define LOG(msg) do { printf("%s\n", msg); } while (0)

typedef struct {
    int x;
    int y;
} Point;

struct Node {
    int value;
    struct Node *next;
};

/* Returns the larger of two ints. */
int max_int(int a, int b)
{
    if (a > b) {
        return a;
    }
    return b;
}

static void print_point(const Point *p)
{
    printf("{%d, %d}\n", p->x, p->y);
    putchar('}');
    putchar('{');
}

// Allocates a node
struct Node *node_new(int value) {
    struct Node *n = malloc(sizeof(struct Node));
    n->value = value;
    n->next = NULL;
    return n;
}

int sum_list(struct Node *head)
{
    int total = 0;
    while (head != NULL) {
        total += head->value;
        head = head->next;
    }
    return total;
}

#ifdef DEBUG
void debug_dump(void)
{
    printf("debug\n");
    printf("more\n");
}
#endif

int main(int argc, char **argv)
{
    Point p = { 1, 2 };
    int arr[] = { 1, 2, 3 };
    print_point(&p);
    for (int i = 0; i < 3; i++) {
        printf("%d\n", arr[i]);
    }
    return 0;
}

enum Color { RED, GREEN, BLUE };
