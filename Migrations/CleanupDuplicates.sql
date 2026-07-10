-- Cleanup script for duplicate NationalId and PhoneNumber values
-- This script keeps the most recent record for each duplicate and nullifies the old ones

-- Clean duplicate NationalId (keep the most recent, nullify others)
WITH DuplicateNationalIds AS (
    SELECT 
        Id,
        NationalId,
        ROW_NUMBER() OVER (PARTITION BY NationalId ORDER BY ApplicationDate DESC, Id DESC) AS RowNum
    FROM StudentProfiles
    WHERE NationalId IS NOT NULL AND NationalId != ''
)
UPDATE StudentProfiles
SET NationalId = NULL
WHERE Id IN (
    SELECT Id FROM DuplicateNationalIds WHERE RowNum > 1
);

-- Clean duplicate PhoneNumber (keep the most recent, nullify others)
WITH DuplicatePhones AS (
    SELECT 
        Id,
        PhoneNumber,
        ROW_NUMBER() OVER (PARTITION BY PhoneNumber ORDER BY ApplicationDate DESC, Id DESC) AS RowNum
    FROM StudentProfiles
    WHERE PhoneNumber IS NOT NULL AND PhoneNumber != ''
)
UPDATE StudentProfiles
SET PhoneNumber = NULL
WHERE Id IN (
    SELECT Id FROM DuplicatePhones WHERE RowNum > 1
);

-- Show results
SELECT 'Duplicate cleanup completed' AS Message;
SELECT COUNT(DISTINCT NationalId) AS UniqueNationalIds, COUNT(*) AS TotalProfiles FROM StudentProfiles WHERE NationalId IS NOT NULL;
SELECT COUNT(DISTINCT PhoneNumber) AS UniquePhones, COUNT(*) AS TotalProfiles FROM StudentProfiles WHERE PhoneNumber IS NOT NULL;
