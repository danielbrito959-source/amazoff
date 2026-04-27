# Amazoff MySQL

Scripts iniciais da base de dados MySQL para o projeto Amazoff.

## Ordem de execucao

1. `001_create_users.sql`
2. `002_add_user_image_path.sql`
3. `003_create_roles.sql`

## Nota de seguranca

A coluna `password_hash` deve guardar apenas hashes de password, por exemplo BCrypt ou Argon2. Nunca guardar passwords em texto simples.
