CREATE TABLE IF NOT EXISTS modelos (
    id INT(11) NOT NULL AUTO_INCREMENT,
    nome VARCHAR(255) NOT NULL,
    id_categoria INT(11) NOT NULL,
    image_path VARCHAR(500) NULL,
    is_active BIT(1) NOT NULL DEFAULT b'1',
    date_created DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    date_changed DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY ix_modelos_id_categoria (id_categoria),
    CONSTRAINT fk_modelos_categorias
        FOREIGN KEY (id_categoria) REFERENCES categorias (id)
        ON DELETE CASCADE
);
