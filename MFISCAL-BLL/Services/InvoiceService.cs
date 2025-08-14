using MFISCAL_DAL.Models;
using MFISCAL_DAL.Repositories;
using MFISCAL_BLL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Services
{
    public class InvoiceService
    {
        private readonly IIdentifiableRepository<InvoiceDB> _invoiceRepo;

        public InvoiceService(IIdentifiableRepository<InvoiceDB> invoiceRepo)
        {
            _invoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
        }

        public async Task<InvoiceDTO?> GetByIdAsync(Guid id)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            return invoice == null ? null : MapToDTO(invoice);
        }

        public IEnumerable<InvoiceDTO> GetAll()
        {
            return _invoiceRepo.GetAllAsReadOnly().Select(MapToDTO).ToList();
        }

        public async Task<InvoiceDTO> CreateAsync(InvoiceDTO dto)
        {
            var invoice = new InvoiceDB
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = dto.InvoiceNumber,
                Amount = dto.Amount,
                Currency = dto.Currency,
                Created = DateTimeOffset.Now,
                Published = null
            };
            _invoiceRepo.Insert(invoice);
            await _invoiceRepo.CommitAsync();
            return MapToDTO(invoice);
        }

        public async Task<InvoiceDTO?> UpdateAsync(Guid id, InvoiceDTO dto)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            if (invoice == null) return null;
            invoice.InvoiceNumber = dto.InvoiceNumber;
            invoice.Amount = dto.Amount;
            invoice.Currency = dto.Currency;
            _invoiceRepo.Update(invoice);
            await _invoiceRepo.CommitAsync();
            return MapToDTO(invoice);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            await _invoiceRepo.DeleteByIdAsync(id);
            await _invoiceRepo.CommitAsync();
            return true;
        }

        public async Task<InvoiceDTO?> PublishAsync(Guid id, DateTimeOffset? publishedAt = null)
        {
            var invoice = _invoiceRepo.GetByIdWithTracking(id);
            if (invoice == null) return null;
            invoice.Published = publishedAt ?? DateTimeOffset.Now;
            _invoiceRepo.Update(invoice);
            await _invoiceRepo.CommitAsync();
            return MapToDTO(invoice);
        }

        private static InvoiceDTO MapToDTO(InvoiceDB db)
        {
            return new InvoiceDTO
            {
                Id = db.Id,
                InvoiceNumber = db.InvoiceNumber,
                Amount = db.Amount,
                Currency = db.Currency
            };
        }
    }
}
