namespace LinkShelf.Core;
public sealed class NaturalTextComparer : IComparer<string>
{
    public static readonly NaturalTextComparer Instance=new();
    public int Compare(string? a,string? b)
    {
        a??="";b??="";int i=0,j=0;
        while(i<a.Length&&j<b.Length)
        {
            if(char.IsAsciiDigit(a[i])&&char.IsAsciiDigit(b[j]))
            {
                int ia=i,jb=j;while(i<a.Length&&char.IsAsciiDigit(a[i]))i++;while(j<b.Length&&char.IsAsciiDigit(b[j]))j++;
                int za=ia,zb=jb;while(za<i&&a[za]=='0')za++;while(zb<j&&b[zb]=='0')zb++;
                int cmp=(i-za).CompareTo(j-zb);if(cmp!=0)return cmp;
                for(int k=0;k<i-za;k++){cmp=a[za+k].CompareTo(b[zb+k]);if(cmp!=0)return cmp;}
                // Numeric value is equal; continue comparing the remaining text.
                continue;
            }
            int c=char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));if(c!=0)return c;i++;j++;
        }
        return (a.Length-i).CompareTo(b.Length-j);
    }
}
