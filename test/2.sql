DECLARE @x INT
SET @x = 1
SELECT * FROM tbl_foo

IF @x = 1
BEGIN
    SELECT * FROM tbl_bar
    IF @x = 2
    BEGIN
        SELECT * FROM tbl_qux
        IF @x = 4
        BEGIN
            SELECT * FROM tbl_garply
            IF @x = 5
            BEGIN
                SELECT * FROM tbl_fred
                IF @x = 6
                BEGIN
                    SELECT * FROM tbl_xyzzy
                END
                ELSE
                BEGIN
                    SELECT * FROM tbl_thud
                END
            END
            ELSE
            BEGIN
                SELECT * FROM tbl_plugh
            END
        END
        ELSE
        BEGIN
            SELECT * FROM tbl_waldo
        END
    END
    ELSE
    BEGIN
        SELECT * FROM tbl_quux
    END
END
ELSE
BEGIN
    SELECT * FROM tbl_baz
    IF @x = 3
    BEGIN
        SELECT * FROM tbl_corge
    END
    ELSE
    BEGIN
        SELECT * FROM tbl_grault
    END
END

EXECUTE sp_executesql N'SELECT * FROM tbl_foo WHERE id = @id', N'@id INT', @id = 1
