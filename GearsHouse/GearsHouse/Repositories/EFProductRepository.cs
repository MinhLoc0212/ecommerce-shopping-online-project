using GearsHouse.Models;
using Microsoft.EntityFrameworkCore;

namespace GearsHouse.Repositories
{
    public class EFProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public EFProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            // return await _context.Products.ToListAsync();
            return await _context.Products
            .Include(p => p.Category) // Include thông tin về category
            .Include(p => p.Brand)    // Include thông tin về brand
            .ToListAsync();
        }
        public async Task<Product> GetByIdAsync(int id)
        {
            return await _context.Products
        .Include(p => p.Category) // Bao gồm thông tin loại sản phẩm
        .Include(p => p.Brand)    // Bao gồm thông tin thương hiệu
        .Include(p => p.Images)   // Bao gồm danh sách ảnh phụ
        .FirstOrDefaultAsync(p => p.Id == id);
        }
        public async Task AddAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }
}
