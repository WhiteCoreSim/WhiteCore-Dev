Setting up translations for the WebUI:

Included in this folder re the current translations that are available plus a skeleton template that can be used for additional translations.

The skeleton.pot can be opened and edited in programs that handle ‘.po’ translation files. i.e. PoEdit.

Check the details of an existing translation if you are unsure on how to proceed.

Once you have your new translation saved as a ‘.po’ file, copy this to the Data/html/translations folder where it will be loaded once the appropriate translator code is created to handle it.

To create the translator program code:

Locate the supplied ‘skeleton’ translator file in the source code at..
<your WhiteCore repo>/WhiteCore/Modules/Web/Translators/SkeletonTranslation.cs
Copy this file and rename it to a name appropriate to your new translation.
i.e. For a Greek translation, copy and rename to >> GreekTranslation.cs

Edit this file in your ride or editor and follow the instructions given in the comments. You will need to supply the language code and name of you translation.

eg. for the Greek translation as above…
    public class SkeletonTranslation : ITranslator  // << Rename for Greek
    public class GreekTranslation : ITranslator

    public string LanguageName {
        get { return "sk"; }                        // << replace the language code
    }
    public string LanguageName {
        get { return “el"; }                        
    }

    public string FullLanguageName {
        get { return "Skeleton"; }                  // << replace the language name
    }
    public string FullLanguageName {
        get { return “Greek”; }                  
    }
 
Save the updated file.
Re-run the appropriate ‘runprebuild.???’ for your operating system (.bat or .sh)
Rebuild and you are ready to go with your new translation.

Questions?
==========
Checkout http://whitecore-sim.org, catch me on the '#whitecore-support' irc channel on freenode,
or check into the MeWe community for WhiteCore https://mewe.com/group/5cb284545da1780ba88ca30d where a friendly group is happy to answer questions.

April 2019
Rowan
<greythane@gmail.com>
