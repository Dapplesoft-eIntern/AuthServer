namespace Web.Api.OtpTemplate;

public static class EmailTemplateRenderer
{
    /// <summary>
    /// Renders an email template by replacing placeholders with actual values
    /// </summary>
    /// <param name="templatePath">Path to the HTML template file</param>
    /// <param name="replacements">Dictionary of placeholder keys and their replacement values</param>
    /// <returns>Rendered HTML string</returns>
    public static string Render(string templatePath, Dictionary<string, string> replacements)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        // Read the template
        string template = File.ReadAllText(templatePath);

        // Replace all placeholders
        foreach (KeyValuePair<string, string> kvp in replacements)
        {
            // Support both {{KEY}} and {KEY} formats
            template = template.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            template = template.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        return template;
    }

    /// <summary>
    /// Renders an email template asynchronously
    /// </summary>
    public static async Task<string> RenderAsync(string templatePath, Dictionary<string, string> replacements, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        // Read the template asynchronously
        string template = await File.ReadAllTextAsync(templatePath, cancellationToken);

        // Replace all placeholders
        foreach (KeyValuePair<string, string> kvp in replacements)
        {
            template = template.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            template = template.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        return template;
    }
}
