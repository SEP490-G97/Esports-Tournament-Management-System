SET NAMES utf8mb4;

START TRANSACTION;

UPDATE Account
SET FullName = 'Quản trị hệ thống'
WHERE Email = 'admin@etoms.local'
  AND BINARY FullName = BINARY 'Qun tr h thng';

UPDATE Game
SET Title = 'Liên Minh Tốc Chiến'
WHERE ShortCode = 'LMTC'
  AND BINARY Title = BINARY 'Lin Minh Tc Chin';

UPDATE Game
SET Title = 'Liên Quân Mobile'
WHERE ShortCode = 'LQM'
  AND BINARY Title = BINARY 'Lin Qun Mobile';

SELECT Email, FullName
FROM Account
WHERE Email = 'admin@etoms.local';

SELECT ShortCode, Title
FROM Game
WHERE ShortCode IN ('LMTC', 'LQM')
ORDER BY ShortCode;

COMMIT;
