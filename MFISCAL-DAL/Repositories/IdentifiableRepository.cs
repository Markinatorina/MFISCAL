using MFISCAL_DAL.Contexts;
using MFISCAL_DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_DAL.Repositories
{
    public class IdentifiableRepository<T> : BaseRepository<T>, IIdentifiableRepository<T> where T : class, IIdentifiableDB
    {
        public IdentifiableRepository(BaseDataContext context) : base(context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context), "Repository: BaseDataContext cannot be null.");
        }

        public T GetById(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            return _entities.AsNoTracking().SingleOrDefault(s => s.Id == id)
                ?? throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
        }

        public T GetByIdWithTracking(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            return _entities.SingleOrDefault(s => s.Id == id)
                ?? throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
        }

        public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            return await _entities.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
        }

        public T GetById(Guid id, params Expression<Func<T, object>>[] includes)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            if (includes == null) throw new ArgumentNullException(nameof(includes), "Repository: includes cannot be null.");
            var query = _entities.AsNoTracking().AsQueryable();

            foreach (var include in includes)
            {
                if (include == null) throw new ArgumentNullException(nameof(include), "Repository: include expression cannot be null.");
                query = query.Include(include);
            }

            return query.SingleOrDefault(s => s.Id == id)
                ?? throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
        }

        public async Task<T> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            if (includes == null) throw new ArgumentNullException(nameof(includes), "Repository: includes cannot be null.");
            var query = _entities.AsNoTracking().AsQueryable();

            foreach (var include in includes)
            {
                if (include == null) throw new ArgumentNullException(nameof(include), "Repository: include expression cannot be null.");
                query = query.Include(include);
            }

            return await query.SingleOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
        }

        public IQueryable<T> GetRangeByIds(IEnumerable<Guid> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids), "Repository: ids cannot be null.");
            var idList = ids.ToList();
            if (!idList.Any())
                return Enumerable.Empty<T>().AsQueryable();

            return _entities.AsNoTracking().Where(entity => idList.Contains(entity.Id));
        }

        public void DeleteById(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            var entity = _entities.SingleOrDefault(s => s.Id == id);
            if (entity != null)
            {
                _entities.Remove(entity);
            }
            else
            {
                throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
            }
        }

        public async Task DeleteByIdAsync(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("Repository: id cannot be empty.", nameof(id));
            var entity = await _entities.SingleOrDefaultAsync(s => s.Id == id);
            if (entity != null)
            {
                _entities.Remove(entity);
            }
            else
            {
                throw new KeyNotFoundException($"Repository: Entity with id {id} not found.");
            }
        }

        public void DeleteRangeByIds(IEnumerable<Guid> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids), "Repository: ids cannot be null.");
            var idList = ids.ToList();
            if (!idList.Any())
                return;
            var entitiesToDelete = _entities.Where(entity => idList.Contains(entity.Id)).ToList();
            if (entitiesToDelete.Any())
            {
                _entities.RemoveRange(entitiesToDelete);
            }
            else
            {
                throw new KeyNotFoundException($"Repository: Entities with ids {string.Join(", ", idList)} not found.");
            }
        }

        public async Task DeleteRangeByIdsAsync(IEnumerable<Guid> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids), "Repository: ids cannot be null.");
            var idList = ids.ToList();
            if (!idList.Any())
                return;
            var entitiesToDelete = await _entities.Where(entity => idList.Contains(entity.Id)).ToListAsync();
            if (entitiesToDelete.Any())
            {
                _entities.RemoveRange(entitiesToDelete);
            }
            else
            {
                throw new KeyNotFoundException($"Repository: Entities with ids {string.Join(", ", idList)} not found.");
            }
        }
    }
}
