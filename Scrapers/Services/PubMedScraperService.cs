using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services
{
    public class PubMedScraperService
    {
        private readonly ClinicalTrialsContext _context;
        private readonly IPubMedClient _pubMedClient;

        public PubMedScraperService(ClinicalTrialsContext context, IPubMedClient pubMedClient)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _pubMedClient = pubMedClient ?? throw new ArgumentNullException(nameof(pubMedClient));
        }

        public async Task<int> IngestPubMedPapersAsync(CancellationToken cancellationToken = default)
        {
            var investigators = await _context.Investigators
                .Where(i => i.Study != null && !i.Study.IsIncomplete)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            int totalPapers = 0;

            foreach (var investigator in investigators)
            {
                var papers = await _pubMedClient.GetPapersForInvestigatorAsync(investigator, cancellationToken).ConfigureAwait(false);

                foreach (var paper in papers)
                {
                    var keywords = ExtractKeywords(paper.Title);

                    var exists = await _context.PubmedStudies.AnyAsync(
                        p => p.InvestigatorId == investigator.Id && p.Title == paper.Title, cancellationToken);
                    if (exists) continue;

                    var pubmedStudy = new PubmedStudyEntity
                    {
                        InvestigatorId = investigator.Id,
                        Title = paper.Title,
                        Url = paper.Url,
                        Keywords = string.Join(",", keywords),
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.PubmedStudies.Add(pubmedStudy);
                }

                if (!string.IsNullOrEmpty(investigator.NcbiId))
                {
                    investigator.NcbiId = investigator.NcbiId;
                }

                if (!string.IsNullOrEmpty(investigator.OrcidId))
                {
                    investigator.OrcidId = investigator.OrcidId;
                }

                investigator.LastSuccessfulPubmedCrawl = DateTime.UtcNow;

                totalPapers += papers.Count;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return totalPapers;
        }

        private IEnumerable<string> ExtractKeywords(string title)
        {
            string[] keywordsOfInterest = new[] { "drug", "therapy", "clinical", "trial", "cancer", "cancerous", "oncology" };
            var foundKeywords = new List<string>();
            var lowerTitle = title.ToLowerInvariant();

            foreach (var keyword in keywordsOfInterest)
            {
                if (lowerTitle.Contains(keyword)) foundKeywords.Add(keyword);
            }

            return foundKeywords.Distinct();
        }
    }
}


