# Learning Journal

## Chapter 2

- The surprising part was that the Domain project can reject an EF Core dependency before any runtime behavior is involved.
- I would ask the architect how the Identity model should be shared without making Persistence depend directly on Infrastructure.
- The Dependency Rule exercise produced `CS0234`: Microsoft.EntityFrameworkCore is unavailable from the Domain project. I removed the invalid using so the Domain project remains persistence-ignorant and builds cleanly.
