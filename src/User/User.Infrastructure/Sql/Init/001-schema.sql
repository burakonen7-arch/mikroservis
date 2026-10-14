CREATE TABLE users
(
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    first_name varchar(100) NOT NULL CHECK (length(trim(first_name)) > 0),
    last_name  varchar(100) NOT NULL CHECK (length(trim(last_name)) > 0),
    email      varchar(254) NOT NULL CHECK (email = lower(trim(email)) AND length(email) > 0),
    age        integer NOT NULL CHECK (age BETWEEN 0 AND 120),
    city       varchar(100) NOT NULL CHECK (length(trim(city)) > 0),
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_users_email UNIQUE (email)
);
