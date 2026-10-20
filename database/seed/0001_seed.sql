-- Fictitious development data: invented titles, @example.com e-mails, generated CPFs (valid check digits).

SET IDENTITY_INSERT dbo.Movie ON;

INSERT INTO dbo.Movie (Id, Title, Genre, ReleaseYear, Rating, PosterUrl, DailyRate, TotalCopies, AvailableCopies) VALUES
    ( 1, N'O Farol de Vidro',          N'Drama',     2019, 4, NULL, 6.90, 3, 2),
    ( 2, N'Código Âmbar',              N'Suspense',  2022, 5, NULL, 8.90, 2, 1),
    ( 3, N'A Última Estação',          N'Ficção',    2015, 3, NULL, 4.90, 1, 1),
    ( 4, N'Risos em Série',            N'Comédia',   2021, 4, NULL, 5.90, 2, 2),
    ( 5, N'Sombras do Cerrado',        N'Aventura',  2018, 4, NULL, 5.90, 1, 0),
    ( 6, N'Doze Minutos',              N'Ação',      2023, 3, NULL, 9.90, 3, 3),
    ( 7, N'Jardim de Inverno',         N'Romance',   2012, 5, NULL, 3.90, 1, 1),
    ( 8, N'Planeta Silencioso',        N'Ficção',    2024, 4, NULL, 9.90, 2, 2),
    ( 9, N'O Caso Marquês',            N'Suspense',  2010, 3, NULL, 3.90, 1, 1),
    (10, N'Maré Alta',                 N'Drama',     2020, 4, NULL, 6.90, 2, 2),
    (11, N'Pequenos Gigantes',         N'Animação',  2017, 5, NULL, 4.90, 3, 3),
    (12, N'Rota 101',                  N'Ação',      2016, 2, NULL, 4.90, 1, 1);

SET IDENTITY_INSERT dbo.Movie OFF;

SET IDENTITY_INSERT dbo.Customer ON;

INSERT INTO dbo.Customer (Id, Name, Email, Cpf, Phone) VALUES
    (1, N'Ana Exemplo',     'ana@example.com',     '39053344705', '11987650001'),
    (2, N'Bruno Fictício',  'bruno@example.com',   '11144477735', '21987650002'),
    (3, N'Carla Teste',     'carla@example.com',   '70212345621', NULL),
    (4, N'Diego Modelo',    'diego@example.com',   '31598765434', '31987650004'),
    (5, N'Elisa Amostra',   'elisa@example.com',   '82045612376', NULL),
    (6, N'Fábio Demo',      'fabio@example.com',   '60432198750', '51987650006');

SET IDENTITY_INSERT dbo.Customer OFF;

-- open (on time), open (overdue), returned (on time) and returned (late)
INSERT INTO dbo.Rental (MovieId, CustomerId, RentedAt, DueAt, ReturnedAt, DailyRate, LateFee, TotalAmount) VALUES
    (1, 1, DATEADD(day, -1, sysutcdatetime()),  DATEADD(day, 2, sysutcdatetime()),  NULL, 6.90, NULL, NULL),
    (2, 2, DATEADD(day, -8, sysutcdatetime()),  DATEADD(day, -5, sysutcdatetime()), NULL, 8.90, NULL, NULL),
    (5, 3, DATEADD(day, -2, sysutcdatetime()),  DATEADD(day, 1, sysutcdatetime()),  NULL, 5.90, NULL, NULL),
    (4, 4, DATEADD(day, -20, sysutcdatetime()), DATEADD(day, -17, sysutcdatetime()), DATEADD(day, -17, sysutcdatetime()), 5.90, 0.00, 17.70),
    (6, 1, DATEADD(day, -15, sysutcdatetime()), DATEADD(day, -12, sysutcdatetime()), DATEADD(day, -10, sysutcdatetime()), 9.90, 29.70, 59.40);
