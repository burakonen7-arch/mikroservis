-- Parameter: @id
SELECT id, first_name, last_name, email, age, city, created_at
FROM users
WHERE id = @id;
