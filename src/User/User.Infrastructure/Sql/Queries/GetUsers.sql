-- Parameters: @search, @city, @minAge, @maxAge, @pageSize, @offset
SELECT id, first_name, last_name, email, age, city, created_at
FROM users
WHERE (@search::text IS NULL
       OR first_name ILIKE '%' || @search || '%'
       OR last_name ILIKE '%' || @search || '%'
       OR email ILIKE '%' || @search || '%')
  AND (@city::text IS NULL OR lower(city) = lower(@city))
  AND (@minAge::integer IS NULL OR age >= @minAge)
  AND (@maxAge::integer IS NULL OR age <= @maxAge)
ORDER BY id
LIMIT @pageSize OFFSET @offset;
