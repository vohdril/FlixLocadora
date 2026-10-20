-- FlixLocadora — domain schema (database-first). The MyFlix catalog (Filme) became a rental shop:
-- movies with copies, customers and rentals. Rental status is DERIVED (open/overdue/returned), never stored.

CREATE TABLE dbo.Movie (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Movie PRIMARY KEY,
    Title           nvarchar(200)  NOT NULL,
    Genre           nvarchar(50)   NOT NULL,
    ReleaseYear     smallint       NOT NULL CONSTRAINT CK_Movie_ReleaseYear CHECK (ReleaseYear BETWEEN 1888 AND 2100),
    Rating          tinyint        NULL     CONSTRAINT CK_Movie_Rating      CHECK (Rating BETWEEN 1 AND 5),
    PosterUrl       varchar(500)   NULL,
    DailyRate       decimal(10, 2) NOT NULL CONSTRAINT CK_Movie_DailyRate   CHECK (DailyRate >= 0),
    TotalCopies     int            NOT NULL CONSTRAINT CK_Movie_TotalCopies CHECK (TotalCopies >= 0),
    AvailableCopies int            NOT NULL,
    IsActive        bit            NOT NULL CONSTRAINT DF_Movie_IsActive  DEFAULT (1),
    CreatedAt       datetime2(0)   NOT NULL CONSTRAINT DF_Movie_CreatedAt DEFAULT (sysutcdatetime()),
    CONSTRAINT CK_Movie_AvailableCopies CHECK (AvailableCopies BETWEEN 0 AND TotalCopies)
);

CREATE TABLE dbo.Customer (
    Id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    Name      nvarchar(200) NOT NULL,
    Email     varchar(254)  NOT NULL CONSTRAINT UQ_Customer_Email UNIQUE,
    Cpf       char(11)      NOT NULL CONSTRAINT UQ_Customer_Cpf   UNIQUE,
    Phone     varchar(15)   NULL,
    IsActive  bit           NOT NULL CONSTRAINT DF_Customer_IsActive  DEFAULT (1),
    CreatedAt datetime2(0)  NOT NULL CONSTRAINT DF_Customer_CreatedAt DEFAULT (sysutcdatetime())
);

CREATE TABLE dbo.Rental (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rental PRIMARY KEY,
    MovieId     int            NOT NULL CONSTRAINT FK_Rental_Movie    REFERENCES dbo.Movie (Id),
    CustomerId  int            NOT NULL CONSTRAINT FK_Rental_Customer REFERENCES dbo.Customer (Id),
    RentedAt    datetime2(0)   NOT NULL,          -- set by the service (TimeProvider), not by a database default
    DueAt       datetime2(0)   NOT NULL,
    ReturnedAt  datetime2(0)   NULL,
    DailyRate   decimal(10, 2) NOT NULL,          -- price frozen when the movie is rented
    LateFee     decimal(10, 2) NULL,
    TotalAmount decimal(10, 2) NULL,              -- filled on return
    CONSTRAINT CK_Rental_Dates CHECK (DueAt > RentedAt AND (ReturnedAt IS NULL OR ReturnedAt >= RentedAt))
);

-- Filtered indexes: the questions the API asks all the time are about OPEN rentals.
CREATE INDEX IX_Rental_Movie_Open    ON dbo.Rental (MovieId)    WHERE ReturnedAt IS NULL;
CREATE INDEX IX_Rental_Customer_Open ON dbo.Rental (CustomerId) WHERE ReturnedAt IS NULL;
