USE amazoff;

ALTER TABLE users
ADD COLUMN image_path VARCHAR(500) NULL AFTER last_name;
