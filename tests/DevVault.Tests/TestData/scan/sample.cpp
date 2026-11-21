#include <vector>

namespace math {
    class VectorOps {
    public:
        // Elemanları toplar
        int sum(const std::vector<int>& values) const {
            int total = 0;
            for (int v : values) total += v;
            return total;
        }
    };
}
