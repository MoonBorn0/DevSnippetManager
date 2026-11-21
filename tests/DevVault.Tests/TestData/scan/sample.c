#include <stdio.h>

struct Point {
    int x;
    int y;
};

// Nokta toplamını hesaplar
int sum_point(struct Point p) {
    int total = p.x + p.y;
    return total;
}
