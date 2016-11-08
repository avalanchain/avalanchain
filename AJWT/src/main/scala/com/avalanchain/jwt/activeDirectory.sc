import java.util.Properties

import javax.naming.Context
import javax.naming.NamingEnumeration
import javax.naming.NamingException
import javax.naming.directory.DirContext
import javax.naming.directory.InitialDirContext
import javax.naming.directory.SearchControls
import javax.naming.directory.SearchResult


val returnAttributes = Array("sAMAccountName", "givenName", "cn", "mail")

def getDomainBase(base: String) =
  base.toUpperCase().split('.').map("DC=" + _).mkString(",")

//val tst = getDomainBase("aaa.sss.dddd")

def activeDirectory(username: String, password: String, domainController: String) {
  val properties = new Properties();

  properties.put(Context.INITIAL_CONTEXT_FACTORY, "com.sun.jndi.ldap.LdapCtxFactory");
  properties.put(Context.PROVIDER_URL, "LDAP://" + domainController);
  properties.put(Context.SECURITY_PRINCIPAL, username + "@" + domainController);
  properties.put(Context.SECURITY_CREDENTIALS, password);

  //initializing active directory LDAP connection
//  try {
//    dirContext = new InitialDirContext(properties);
//  } catch (NamingException e) {
//    LOG.severe(e.getMessage());
//  }

  val dirContext = new InitialDirContext(properties)

  //default domain base for search
  val domainBase = getDomainBase(domainController)

  //initializing search controls
  val searchCtls = new SearchControls()
  searchCtls.setSearchScope(SearchControls.SUBTREE_SCOPE)
  searchCtls.setReturningAttributes(returnAttributes)
}

def enc(s: String, i: Byte, d: Int) = {
  (i until (s.length + i)).
    zip(s.toCharArray).
    map(p => p._2 + p._1 * 2 * d).
    map(_.toChar).
    mkString
}

//val enced = enc("asd11", 3, 1)
//val deced = enc(enced, 3, -1)

def pwd() = enc("", 3, -1)

